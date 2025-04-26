/** @file
  Copyright (c) 2010, Ekon Benefits.
  Copyright (c) 2025, Cory Bennett. All rights reserved.
  SPDX-License-Identifier: Apache-2.0
**/
#pragma warning disable CS0108

using System.Collections;
using System.Collections.ObjectModel;
using System.Linq.Expressions;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;

using Microsoft.CSharp.RuntimeBinder;

using Dynamitey;

using ImpromptuInterface;
using ImpromptuInterface.Build;


namespace MTGOSDK.Core.Reflection.Proxy.Builder;

public class TypeAssembler //: ActLikeMaker
{
  // public dynamic ActLike(object originalDynamic, params Type[] otherInterfaces)
  // {
  //   return new ProxyCaster(originalDynamic, otherInterfaces) {Maker = this};
  // }

  public TInterface Create<TTarget, TInterface>() where TTarget : new() where TInterface : class
  {
    return TypeProxy<TInterface>.As(new TTarget());
  }

  public TInterface Create<TTarget, TInterface>(params object[] args) where TInterface : class
  {
    return TypeProxy<dynamic>.As(Dynamic.InvokeConstructor(typeof(TTarget), args));
  }

  protected ModuleBuilder _builder;

  protected AssemblyBuilder _ab;
  protected readonly IDictionary<TypeHash, Type> _typeHash = new Dictionary<TypeHash, Type>();
  protected readonly object TypeCacheLock = new object();

  protected readonly IDictionary<TypeHash, Type> _delegateCache = new Dictionary<TypeHash, Type>();
  protected readonly object DelegateCacheLock = new object();

  protected readonly MethodInfo ActLikeRec = typeof(TypeAssembler).GetMethod(nameof(RecursiveActLikeForProxy),
    new[] {typeof(TypeAssembler), typeof(object) });

  public static TInterface RecursiveActLikeForProxy<TInterface>(TypeAssembler maker, object target) where TInterface : class
  {
    // return maker.ActLike<TInterface>(target);
    return TypeProxy<TInterface>.As(target);
  }

  public string AssemblyName { get; private set; }

  public AssemblyBuilderAccess AssemblyAccess { get; private set; }

  internal TypeAssembler()
  {
    AssemblyAccess = AssemblyBuilderAccess.Run;
    AssemblyName = DynamicTypeBuilder.s_assemblyName.FullName;
  }

  public TypeAssembler(AssemblyBuilderAccess access, string assemblyName = null)
  {
    AssemblyAccess = access;
    if (assemblyName == null)
    {
      assemblyName = DynamicTypeBuilder.s_assemblyName.FullName;
    }

    AssemblyName = assemblyName;
  }

  ///<summary>
  /// Module Builder for building proxies
  ///</summary>
  private ModuleBuilder Builder => DynamicTypeBuilder.s_builder;

  /// <summary>
  /// Preloads a proxy for ActLike to use.
  /// </summary>
  /// <param name="proxyType">Type of the proxy.</param>
  /// <param name="attribute">The ActLikeProxyAttribute, if not provide it will be looked up.</param>
  /// <returns>Returns false if there already is a proxy registered for the same type.</returns>
  public bool PreLoadProxy(Type proxyType, ActLikeProxyAttribute attribute = null)
  {
    var tSuccess = true;
    if (attribute == null)
      attribute = proxyType.GetCustomAttributes(typeof(ActLikeProxyAttribute), inherit: false).Cast<ActLikeProxyAttribute>().FirstOrDefault();

    if (attribute == null)
      throw new Exception("Proxy Type must have ActLikeProxyAttribute");

    if (!typeof(IProxyInitialize).IsAssignableFrom(proxyType))
      throw new Exception("Proxy Type must implement IProxyInitialize");

    foreach (var tIType in attribute.Interfaces)
    {
      if (!tIType.IsAssignableFrom(proxyType))
      {
        throw new Exception(String.Format("Proxy Type {0} must implement declared interfaces {1}", proxyType, tIType));
      }
    }

    lock (TypeCacheLock)
    {
      var tNewHash = TypeHash.Create(attribute.Context, attribute.Interfaces);

      if (!_typeHash.ContainsKey(tNewHash))
      {
        _typeHash[tNewHash] = proxyType;
      }
      else
      {
        tSuccess = false;
      }
    }
    return tSuccess;
  }

  /// <summary>
  /// Preloads proxies that ActLike uses from assembly.
  /// </summary>
  /// <param name="assembly">The assembly.</param>
  /// <returns>Returns false if there already is a proxy registered for the same type.</returns>
  public bool PreLoadProxiesFromAssembly(Assembly assembly)
  {
    var tSuccess = true;
    var typesWithMyAttribute =
      from tType in assembly.GetTypes()
      let tAttributes = tType.GetCustomAttributes(typeof(ActLikeProxyAttribute), inherit: false)
      where tAttributes != null && tAttributes.Length == 1
      select new { Type = tType, Impromptu = tAttributes.Cast<ActLikeProxyAttribute>().Single() };
    foreach (var tTypeCombo in typesWithMyAttribute)
    {
      lock (TypeCacheLock)
      {
        if (!PreLoadProxy(tTypeCombo.Type, tTypeCombo.Impromptu))
          tSuccess = false;
      }
    }
    return tSuccess;
  }

  public void MakePropertyDescribedProperty(ModuleBuilder builder, TypeBuilder typeBuilder, Type contextType, string tName, Type tReturnType)
  {
    var tGetName = "get_" + tName;

    var tEmitInfo = new PropertyEmitInfo
      { Name = tName, GetName = tGetName, DefaultInterfaceImplementation = true, ContextType = contextType, ResolveReturnType = tReturnType };

    MakePropertyHelper(builder, typeBuilder, tEmitInfo);
  }

  private static Type DefineCallsiteFieldForMethod(TypeBuilder builder, TypeAssembler maker, string name, Type returnType, IEnumerable<Type> argTypes, MethodInfo info)
  {
    Type tFuncType = maker.GenerateCallSiteFuncType(argTypes, returnType, info, builder);
    Type tReturnType = typeof(CallSite<>).MakeGenericType(tFuncType);

    builder.DefineField(name, tReturnType, FieldAttributes.Static | FieldAttributes.Public);
    return tFuncType;
  }


  internal static Type DefineCallsiteField(TypeBuilder builder, TypeAssembler maker, string name, Type returnType, params Type[] argTypes)
  {
    Type tFuncType = maker.GenerateCallSiteFuncType(argTypes, returnType);
    Type tReturnType = typeof(CallSite<>).MakeGenericType(tFuncType);

    builder.DefineField(name, tReturnType, FieldAttributes.Static | FieldAttributes.Public);
    return tFuncType;
  }

  private class MethodSigHash
  {
    public readonly string Name;
    public readonly Type[] Parameters;

    public MethodSigHash(MethodInfo info)
    {
      Name = info.Name;
      Parameters = info.GetParameters().Select(it => it.ParameterType).ToArray();
    }

    public MethodSigHash(string name, Type[] parameters)
    {
      Name = name;
      Parameters = parameters;
    }

    public bool Equals(MethodSigHash other)
    {
      if (ReferenceEquals(null, other)) return false;
      if (ReferenceEquals(this, other)) return true;
      return Equals(other.Name, Name) && StructuralComparisons.StructuralEqualityComparer.Equals(other.Parameters, Parameters);
    }

    public override bool Equals(object obj)
    {
      if (ReferenceEquals(null, obj)) return false;
      if (ReferenceEquals(this, obj)) return true;
      if (obj.GetType() != typeof(MethodSigHash)) return false;
      return Equals((MethodSigHash)obj);
    }

    public override int GetHashCode()
    {
      unchecked
      {
        return (Name.GetHashCode() * 397) ^ StructuralComparisons.StructuralEqualityComparer.GetHashCode(Parameters);
      }
    }
  }

  private IEnumerable<Type> FlattenGenericParameters(Type type)
  {
    if (type.IsByRef || type.IsArray || type.IsPointer)
    {
      return FlattenGenericParameters(type.GetElementType());
    }

    if (type.IsGenericParameter)
      return new[] { type };
    if (type.ContainsGenericParameters)
    {
      return type.GetGenericArguments().SelectMany(FlattenGenericParameters);
    }
    return new Type[] { };
  }

  private Type ReplaceTypeWithGenericBuilder(Type type, IDictionary<Type, GenericTypeParameterBuilder> dict)
  {
    var tStartType = type;
    Type tReturnType;
    if (type.IsByRef || type.IsArray || type.IsPointer)
    {
      tStartType = type.GetElementType();
    }

    if (tStartType.IsGenericType)
    {
      var tArgs = tStartType.GetGenericArguments().Select(it => ReplaceTypeWithGenericBuilder(it, dict));

      var tNewType = tStartType.GetGenericTypeDefinition().MakeGenericType(tArgs.ToArray());
      tReturnType = tNewType;
    }
    else if (dict.ContainsKey(tStartType))
    {
      var tNewType = dict[tStartType];
      var tAttributes = tStartType.GenericParameterAttributes;
      tNewType.SetGenericParameterAttributes(tAttributes);
      foreach (var tConstraint in tStartType.GetGenericParameterConstraints())
      {
        if (tConstraint.IsInterface)
          tNewType.SetInterfaceConstraints(tConstraint);
        else
          tNewType.SetBaseTypeConstraint(tConstraint);
      }
      tReturnType = tNewType;
    }
    else
    {
      tReturnType = tStartType;
    }

    if (type.IsByRef)
    {
      return tReturnType.MakeByRefType();
    }

    if (type.IsArray)
    {
      return tReturnType.MakeArrayType();
    }

    if (type.IsPointer)
    {
      return tReturnType.MakePointerType();
    }

    return tReturnType;
  }

  private object CustomAttributeTypeArgument(CustomAttributeTypedArgument argument)
  {
    if (argument.Value is ReadOnlyCollection<CustomAttributeTypedArgument>)
    {
      var tValue = argument.Value as ReadOnlyCollection<CustomAttributeTypedArgument>;
      return
        new ArrayList(tValue.Select(it => it.Value).ToList()).ToArray(argument.ArgumentType.GetElementType());
    }

    return argument.Value;
  }


  public CustomAttributeBuilder GetAttributeBuilder(CustomAttributeData data)
  {
    var tPropertyInfos = new List<PropertyInfo>();
    var tPropertyValues = new List<object>();
    var tFieldInfos = new List<FieldInfo>();
    var tFieldValues = new List<object>();

    if (data.NamedArguments != null)
    {
      foreach (var namedArg in data.NamedArguments)
      {
        var fi = namedArg.MemberInfo as FieldInfo;
        var pi = namedArg.MemberInfo as PropertyInfo;

        if (fi != null)
        {
          tFieldInfos.Add(fi);
          tFieldValues.Add(namedArg.TypedValue.Value);
        }
        else if (pi != null)
        {
          tPropertyInfos.Add(pi);
          tPropertyValues.Add(namedArg.TypedValue.Value);
        }
      }
    }

    return new CustomAttributeBuilder(
      data.Constructor,
      data.ConstructorArguments.Select(CustomAttributeTypeArgument).ToArray(),
      tPropertyInfos.ToArray(),
      tPropertyValues.ToArray(),
      tFieldInfos.ToArray(),
      tFieldValues.ToArray());
  }

  public void MakeMethod(ModuleBuilder builder, MethodInfo info, TypeBuilder typeBuilder, Type contextType, bool nonRecursive = false, bool defaultImp = true)
  {
    var tEmitInfo = new MethodEmitInfo { Name = info.Name, DefaultInterfaceImplementation = defaultImp, NonRecursive = nonRecursive };

    if (info.GetCustomAttributes(typeof(AliasAttribute), false).FirstOrDefault() is AliasAttribute alias)
    {
      tEmitInfo.Alias = alias.Name;
    }

    var tParamAttri = info.GetParameters();
    Type[] tParamTypes = tParamAttri.Select(it => it.ParameterType).ToArray();


    IEnumerable<string> tArgNames;
    if (info.GetCustomAttributes(typeof(UseNamedArgumentAttribute), false).Any() ||
      info.DeclaringType.GetCustomAttributes(typeof(UseNamedArgumentAttribute), false).Any())

    {
      tArgNames = tParamAttri.Select(it => it.Name).ToList();
    }
    else
    {
      var tParam = tParamAttri.Zip(Enumerable.Range(0, tParamTypes.Count()), (p, i) => new { i, p })
        .FirstOrDefault(it => it.p.GetCustomAttributes(typeof(UseNamedArgumentAttribute), false).Any());

      tArgNames = tParam == null
        ? Enumerable.Repeat(default(string), tParamTypes.Length)
        : Enumerable.Repeat(default(string), tParam.i).Concat(tParamAttri.Skip(Math.Min(tParam.i - 1, 0)).Select(it => it.Name)).ToList();
    }

    var tReturnType = typeof(void);
    if (info.ReturnParameter != null)
      tReturnType = info.ReturnParameter.ParameterType;

    var tCallSiteName = tEmitInfo.CallSiteName;
    var tCStp = DefineBuilderForCallSite(builder, tCallSiteName);

    var tReplacedTypes = GetParamTypes(tCStp, info);
    if (tReplacedTypes != null)
    {
      tReturnType = tReplacedTypes.Item1;
      tParamTypes = tReplacedTypes.Item2;
    }

    var tConvert = tEmitInfo.CallSiteConvertName;
    Type tConvertFuncType = null;
    if (tReturnType != typeof(void))
    {
      tConvertFuncType = DefineCallsiteField(tCStp, this, tConvert, tReturnType);
    }

    var tInvokeMethod = tEmitInfo.CallSiteInvokeName;
    var tInvokeFuncType = DefineCallsiteFieldForMethod(tCStp, this, tInvokeMethod, tReturnType != typeof(void) ? typeof(object) : typeof(void), tParamTypes, info);

    Type tCallSite = tCStp.CreateTypeInfo();

    var tPublicPrivate = MethodAttributes.Public;
    var tPrefixName = tEmitInfo.Name;
    if (!tEmitInfo.DefaultInterfaceImplementation)
    {
      tPrefixName = String.Format("{0}.{1}", info.DeclaringType.FullName, tPrefixName);

      tPublicPrivate = MethodAttributes.Private;
    }

    var tMethodBuilder = typeBuilder.DefineMethod(tPrefixName,
      tPublicPrivate | MethodAttributes.HideBySig | MethodAttributes.Virtual | MethodAttributes.Final | MethodAttributes.NewSlot);

    tReplacedTypes = GetParamTypes(tMethodBuilder, info);
    var tReducedParams = tParamTypes.Select(ReduceToElementType).ToArray();
    if (tReplacedTypes != null)
    {
      tReturnType = tReplacedTypes.Item1;
      tParamTypes = tReplacedTypes.Item2;

      tReducedParams = tParamTypes.Select(ReduceToElementType).ToArray();
      var tGenericParams = tMethodBuilder.GetGenericArguments();
      tCallSite = tCallSite.GetGenericTypeDefinition().MakeGenericType(tGenericParams);
      if (tConvertFuncType != null)
        tConvertFuncType = UpdateCallsiteFuncType(tConvertFuncType, tReturnType);
      tInvokeFuncType = UpdateCallsiteFuncType(tInvokeFuncType, tReturnType != typeof(void) ? typeof(object) : typeof(void), tReducedParams);
    }

    tMethodBuilder.SetReturnType(tReturnType);
    tMethodBuilder.SetParameters(tParamTypes);

    foreach (var tParam in info.GetParameters())
    {
      var tParamBuilder = tMethodBuilder.DefineParameter(tParam.Position + 1, AttributesForParam(tParam), tParam.Name);

      var tCustomAttributes = tParam.GetCustomAttributesData();
      foreach (var tCustomAttribute in tCustomAttributes)
      {
        try
        {
          tParamBuilder.SetCustomAttribute(GetAttributeBuilder(tCustomAttribute));
        }
        catch { }//For most proxies not having the same attributes won't really matter,
        //but just incase we don't want to stop for some unknown attribute that we can't initialize
      }

    }

    if (!tEmitInfo.DefaultInterfaceImplementation)
    {
      typeBuilder.DefineMethodOverride(tMethodBuilder, info);
    }
    tEmitInfo.ResolvedParamTypes = tReducedParams;
    tEmitInfo.ResolveReturnType = tReturnType;
    tEmitInfo.CallSiteType = tCallSite;
    tEmitInfo.ContextType = contextType;
    tEmitInfo.CallSiteConvertFuncType = tConvertFuncType;
    tEmitInfo.CallSiteInvokeFuncType = tInvokeFuncType;
    tEmitInfo.ArgNames = tArgNames;
    EmitMethodBody(tMethodBuilder, tParamAttri, tEmitInfo);
  }

  private TypeBuilder DefineBuilderForCallSite(ModuleBuilder builder, string tCallSiteInvokeName)
  {
    return builder.DefineType(tCallSiteInvokeName,

      TypeAttributes.NotPublic
      | TypeAttributes.Sealed
      | TypeAttributes.AutoClass
      | TypeAttributes.BeforeFieldInit
      | TypeAttributes.Abstract
    );
  }

  internal Tuple<Type, Type[]> GetParamTypes(dynamic builder, MethodInfo info)
  {
    if (info == null)
      return null;

    var paramTypes = info.GetParameters().Select(it => it.ParameterType).ToArray();
    var returnType = typeof(void);
    if (info.ReturnParameter != null)
      returnType = info.ReturnParameter.ParameterType;

    var tGenericParams = info.GetGenericArguments()
      .SelectMany(FlattenGenericParameters)
      .Distinct().ToDictionary(it => it.GenericParameterPosition, it => new { Type = it, Gen = default(GenericTypeParameterBuilder) });
    var tParams = tGenericParams;
    var tReturnParameters = FlattenGenericParameters(returnType).Where(it => !tParams.ContainsKey(it.GenericParameterPosition));
    foreach (var tReParm in tReturnParameters)
      tGenericParams.Add(tReParm.GenericParameterPosition, new { Type = tReParm, Gen = default(GenericTypeParameterBuilder) });
    var tGenParams = tGenericParams.OrderBy(it => it.Key).Select(it => it.Value.Type.Name);
    if (tGenParams.Any())
    {
      GenericTypeParameterBuilder[] tBuilders = builder.DefineGenericParameters(tGenParams.ToArray());
      var tDict = tGenericParams.ToDictionary(param => param.Value.Type, param => tBuilders[param.Key]);

      returnType = ReplaceTypeWithGenericBuilder(returnType, tDict);
      if (tDict.ContainsKey(returnType))
      {
        returnType = tDict[returnType];
      }
      paramTypes = paramTypes.Select(it => ReplaceTypeWithGenericBuilder(it, tDict)).ToArray();
      return Tuple.Create(returnType, paramTypes);
    }
    return null;
  }

  private void EmitMethodBody(
    MethodBuilder methodBuilder,
    ParameterInfo[] paramInfo,
    MethodEmitInfo emitInfo
  )
  {
    var tIlGen = methodBuilder.GetILGenerator();

    var tConvertField = emitInfo.CallSiteType.GetFieldEvenIfGeneric(emitInfo.CallSiteConvertName);
    if (emitInfo.ResolveReturnType != typeof(void))
    {
      using (tIlGen.EmitBranchTrue(gen => gen.Emit(OpCodes.Ldsfld, tConvertField)))
      {
        tIlGen.EmitDynamicConvertBinder(CSharpBinderFlags.None, emitInfo.ResolveReturnType, emitInfo.ContextType);
        tIlGen.EmitCallsiteCreate(emitInfo.CallSiteConvertFuncType);
        tIlGen.Emit(OpCodes.Stsfld, tConvertField);
      }
    }

    var tInvokeField = emitInfo.CallSiteType.GetFieldEvenIfGeneric(emitInfo.CallSiteInvokeName);

    using (tIlGen.EmitBranchTrue(gen => gen.Emit(OpCodes.Ldsfld, tInvokeField)))
    {
      tIlGen.EmitDynamicMethodInvokeBinder(
        emitInfo.ResolveReturnType == typeof(void) ? CSharpBinderFlags.ResultDiscarded : CSharpBinderFlags.None,
        emitInfo.Alias ?? emitInfo.Name,
        methodBuilder.GetGenericArguments(),
        emitInfo.ContextType,
        paramInfo,
        emitInfo.ArgNames);
      tIlGen.EmitCallsiteCreate(emitInfo.CallSiteInvokeFuncType);
      tIlGen.Emit(OpCodes.Stsfld, tInvokeField);
    }

    //If it's an interface and not nonrecursive this will be true
    var tRecurse = emitInfo.ResolveReturnType.IsInterface && !emitInfo.NonRecursive;

    if (emitInfo.ResolveReturnType != typeof(void) && !tRecurse)
    {
      tIlGen.Emit(OpCodes.Ldsfld, tConvertField);
      tIlGen.Emit(OpCodes.Ldfld, typeof(CallSite<>).MakeGenericType(emitInfo.CallSiteConvertFuncType).GetFieldEvenIfGeneric("Target"));
      tIlGen.Emit(OpCodes.Ldsfld, tConvertField);
    }

    tIlGen.Emit(OpCodes.Ldsfld, tInvokeField);
    tIlGen.Emit(OpCodes.Ldfld, typeof(CallSite<>).MakeGenericType(emitInfo.CallSiteInvokeFuncType).GetFieldEvenIfGeneric("Target"));
    tIlGen.Emit(OpCodes.Ldsfld, tInvokeField);
    tIlGen.Emit(OpCodes.Ldarg_0);
    tIlGen.Emit(OpCodes.Callvirt, typeof(IProxy).GetProperty(nameof(IProxy.Original)).GetGetMethod());
    for (var i = 1; i <= emitInfo.ResolvedParamTypes.Length; i++)
    {
      tIlGen.EmitLoadArgument(i);
    }
    tIlGen.EmitCallInvokeFunc(emitInfo.CallSiteInvokeFuncType);
    if (emitInfo.ResolveReturnType != typeof(void) && !tRecurse)
    {
      tIlGen.EmitCallInvokeFunc(emitInfo.CallSiteConvertFuncType);
    }

    //If we are recursing, try actlike
    if (tRecurse)
    {
      var tReturnLocal = tIlGen.DeclareLocal(typeof(object));
      tIlGen.EmitStoreLocation(tReturnLocal.LocalIndex);
      using (tIlGen.EmitBranchFalse(gen => gen.EmitLoadLocation(tReturnLocal.LocalIndex)))
      {
        tIlGen.EmitLoadLocation(tReturnLocal.LocalIndex);
        using (tIlGen.EmitBranchTrue(gen => gen.Emit(OpCodes.Isinst, emitInfo.ResolveReturnType)))
        {
          tIlGen.Emit(OpCodes.Ldarg_0);
          tIlGen.Emit(OpCodes.Callvirt, typeof(IProxy).GetProperty(nameof(IProxy.Maker)).GetGetMethod());
          tIlGen.EmitLoadLocation(tReturnLocal.LocalIndex);
          tIlGen.Emit(OpCodes.Call, ActLikeRec.MakeGenericMethod(emitInfo.ResolveReturnType));
          tIlGen.Emit(OpCodes.Ret);
        }
      }

      tIlGen.Emit(OpCodes.Ldsfld, tConvertField);
      tIlGen.Emit(OpCodes.Ldfld, typeof(CallSite<>).MakeGenericType(emitInfo.CallSiteConvertFuncType).GetFieldEvenIfGeneric("Target"));
      tIlGen.Emit(OpCodes.Ldsfld, tConvertField);
      tIlGen.EmitLoadLocation(tReturnLocal.LocalIndex);
      tIlGen.EmitCallInvokeFunc(emitInfo.CallSiteConvertFuncType);
    }

    tIlGen.Emit(OpCodes.Ret);
  }


  public void MakeProperty(ModuleBuilder builder, PropertyInfo info, TypeBuilder typeBuilder, Type contextType, bool nonRecursive = false, bool defaultImp = true)
  {
    var tEmitInfo = new PropertyEmitInfo()
    {
      Name = info.Name,
      ResolveReturnType = info.PropertyType,
      GetName = info.CanRead ? info.GetGetMethod().Name : null, // q: getter may be missing!
      SetName = info.CanWrite ? info.GetSetMethod().Name : null, // q: setter may be missing!
      ContextType = contextType,
      DefaultInterfaceImplementation = defaultImp,
      NonRecursive = nonRecursive
    };

    if (info.GetCustomAttributes(typeof(AliasAttribute), false).FirstOrDefault() is AliasAttribute alias)
    {
      tEmitInfo.Alias = alias.Name;
    }

    MakePropertyHelper(builder, typeBuilder, tEmitInfo, info);
  }

  private class PropertyEmitInfo : EmitInfo
  {
    public PropertyEmitInfo()
    {
      CallSiteConvertName = "Convert_Get";
      CallSiteInvokeGetName = "Invoke_Get";
      CallSiteInvokeSetName = "Invoke_Set";
    }
    public string GetName { get; set; }
    public string SetName { get; set; }

    public string CallSiteInvokeSetName { get; protected set; }
    public string CallSiteInvokeGetName { get; protected set; }
    public string CallSiteConvertName { get; protected set; }

    public Type[] ResolvedIndexParamTypes { get; set; }
    public Type CallSiteConvertFuncType { get; set; }
    public Type CallSiteInvokeGetFuncType { get; set; }
    public Type CallSiteInvokeSetFuncType { get; set; }
  }

  private class EmitEventInfo : PropertyEmitInfo
  {
    public string CallSiteIsEventName { get; protected set; }
    public string CallSiteAddAssignName { get; protected set; }
    public string CallSiteSubtractAssignName { get; protected set; }
    public string CallSiteAddName { get; protected set; }
    public string CallSiteRemoveName { get; protected set; }

    public Type CallSiteIsEventFuncType { get; set; }
    public Type CallSiteAddAssignFuncType { get; set; }
    public Type CallSiteSubtractAssignFuncType { get; set; }
    public Type CallSiteAddFuncType { get; set; }
    public Type CallSiteRemoveFuncType { get; set; }
    public Type[] ResolvedAddParamTypes { get; set; }
    public Type[] ResolvedRemoveParamTypes { get; set; }

    public EmitEventInfo()
    {
      CallSiteIsEventName = "Invoke_IsEvent";
      CallSiteAddAssignName = "Invoke_AddAssign";
      CallSiteSubtractAssignName = "Invoke_SubtractAssign";
      CallSiteAddName = "Invoke_Add";
      CallSiteRemoveName = "Invoke_Remove";
    }
  }

  public void MakeEvent(ModuleBuilder builder, EventInfo info, TypeBuilder typeBuilder, Type contextType, bool defaultImp)
  {
    var tEmitInfo = new EmitEventInfo
    {
      Name = info.Name,
      ContextType = contextType,
      DefaultInterfaceImplementation = defaultImp
    };
    if (info.GetCustomAttributes(typeof(AliasAttribute), false).FirstOrDefault() is AliasAttribute alias)
    {
      tEmitInfo.Alias = alias.Name;
    }

    var tAddMethod = info.GetAddMethod();
    var tRemoveMethod = info.GetRemoveMethod();
    tEmitInfo.ResolveReturnType = info.EventHandlerType;

    var tCStp = DefineBuilderForCallSite(builder, tEmitInfo.CallSiteName);

    tEmitInfo.CallSiteIsEventFuncType = DefineCallsiteField(tCStp, this, tEmitInfo.CallSiteIsEventName, typeof(bool));

    tEmitInfo.CallSiteAddAssignFuncType = DefineCallsiteField(tCStp, this, tEmitInfo.CallSiteAddAssignName, typeof(object), tEmitInfo.ResolveReturnType);

    tEmitInfo.CallSiteSubtractAssignFuncType = DefineCallsiteField(tCStp, this, tEmitInfo.CallSiteSubtractAssignName, typeof(object), tEmitInfo.ResolveReturnType);

    tEmitInfo.ResolvedAddParamTypes = tRemoveMethod.GetParameters().Select(it => it.ParameterType).ToArray();
    tEmitInfo.CallSiteAddFuncType = DefineCallsiteField(tCStp, this, tEmitInfo.CallSiteAddName, typeof(object), tEmitInfo.ResolvedAddParamTypes);

    tEmitInfo.ResolvedRemoveParamTypes = tRemoveMethod.GetParameters().Select(it => it.ParameterType).ToArray();
    tEmitInfo.CallSiteRemoveFuncType = DefineCallsiteField(tCStp, this, tEmitInfo.CallSiteRemoveName, typeof(object), tEmitInfo.ResolvedRemoveParamTypes);

    tEmitInfo.CallSiteInvokeGetFuncType = DefineCallsiteField(tCStp, this, tEmitInfo.CallSiteInvokeGetName, typeof(object));

    tEmitInfo.CallSiteInvokeSetFuncType = DefineCallsiteField(tCStp, this, tEmitInfo.CallSiteInvokeSetName, typeof(object), typeof(object));

    tEmitInfo.CallSiteType = tCStp.CreateTypeInfo();

    var tMp = typeBuilder.DefineEvent(info.Name, EventAttributes.None, tEmitInfo.ResolveReturnType);

    var tSetField = tEmitInfo.CallSiteType.GetFieldEvenIfGeneric(tEmitInfo.CallSiteInvokeSetName);
    var tGetField = tEmitInfo.CallSiteType.GetFieldEvenIfGeneric(tEmitInfo.CallSiteInvokeGetName);
    var tIsEventField = tEmitInfo.CallSiteType.GetFieldEvenIfGeneric(tEmitInfo.CallSiteIsEventName);

    var tPublicPrivate = MethodAttributes.Public;
    if (!tEmitInfo.DefaultInterfaceImplementation)
    {
      tPublicPrivate = MethodAttributes.Private;
    }

    //AddMethod
    var tAddPrefixName = tAddMethod.Name;
    if (!tEmitInfo.DefaultInterfaceImplementation)
    {
      tAddPrefixName = $"{info.DeclaringType.FullName}.{tAddPrefixName}";
    }

    var tAddBuilder = typeBuilder.DefineMethod(tAddPrefixName,
      tPublicPrivate | MethodAttributes.SpecialName | MethodAttributes.HideBySig | MethodAttributes.Virtual | MethodAttributes.Final | MethodAttributes.NewSlot,
      typeof(void),
      tEmitInfo.ResolvedAddParamTypes);

    if (!tEmitInfo.DefaultInterfaceImplementation)
    {
      typeBuilder.DefineMethodOverride(tAddBuilder, info.GetAddMethod());
    }

    foreach (var tParam in tAddMethod.GetParameters())
    {
      tAddBuilder.DefineParameter(tParam.Position + 1, AttributesForParam(tParam), tParam.Name);
    }

    EmitAddEvent(tMp, tAddBuilder, info, tAddMethod, tGetField, tSetField, tIsEventField, tEmitInfo);

    //Remove Method
    var tRemovePrefixName = tRemoveMethod.Name;
    if (!tEmitInfo.DefaultInterfaceImplementation)
    {
      tRemovePrefixName = $"{info.DeclaringType.FullName}.{tRemovePrefixName}";
    }
    var tRemoveBuilder = typeBuilder.DefineMethod(tRemovePrefixName,
      tPublicPrivate
      | MethodAttributes.SpecialName
      | MethodAttributes.HideBySig
      | MethodAttributes.Virtual
      | MethodAttributes.Final
      | MethodAttributes.NewSlot,
      typeof(void),
      tEmitInfo.ResolvedAddParamTypes);
    if (!tEmitInfo.DefaultInterfaceImplementation)
    {
      typeBuilder.DefineMethodOverride(tRemoveBuilder, info.GetRemoveMethod());
    }

    foreach (var tParam in tRemoveMethod.GetParameters())
    {
      tRemoveBuilder.DefineParameter(tParam.Position + 1, AttributesForParam(tParam), tParam.Name);
    }

    EmitRemoveEvent(tMp, tRemoveBuilder, info, tRemoveMethod, tGetField, tSetField, tIsEventField, tEmitInfo);
  }

  private void EmitRemoveEvent(EventBuilder tMp, MethodBuilder tRemoveBuilder, EventInfo info, MethodInfo tRemoveMethod,
    FieldInfo tGetField, FieldInfo tSetField, FieldInfo tIsEventField,
    EmitEventInfo tEmitInfo)
  {
    var tIlGen = tRemoveBuilder.GetILGenerator();

    using (tIlGen.EmitBranchTrue(load => load.Emit(OpCodes.Ldsfld, tIsEventField)))
    {
      tIlGen.EmitDynamicIsEventBinder(CSharpBinderFlags.None, tEmitInfo.Alias ?? tEmitInfo.Name, tEmitInfo.ContextType);
      tIlGen.EmitCallsiteCreate(tEmitInfo.CallSiteIsEventFuncType);
      tIlGen.Emit(OpCodes.Stsfld, tIsEventField);
    }

    using (tIlGen.EmitBranchTrue(
            load => load.EmitInvocation(
              target => target.EmitInvocation(
                t => t.Emit(OpCodes.Ldsfld, tIsEventField),
                i => i.Emit(OpCodes.Ldfld, tIsEventField.FieldType.GetFieldEvenIfGeneric("Target"))
              ),
              invoke => invoke.EmitCallInvokeFunc(tEmitInfo.CallSiteIsEventFuncType),
              param => param.Emit(OpCodes.Ldsfld, tIsEventField),
              param => param.EmitInvocation(
                t => t.Emit(OpCodes.Ldarg_0),
                i => i.Emit(OpCodes.Callvirt, typeof(IProxy).GetProperty("Original").GetGetMethod())
              )
            )
          )
        ) //if IsEvent Not True
    {
      using (tIlGen.EmitBranchTrue(gen => gen.Emit(OpCodes.Ldsfld, tSetField)))
      {
        tIlGen.EmitDynamicSetBinderDynamicParams(CSharpBinderFlags.ValueFromCompoundAssignment, tEmitInfo.Alias ?? tEmitInfo.Name,
          tEmitInfo.ContextType, tEmitInfo.ResolveReturnType);
        tIlGen.EmitCallsiteCreate(tEmitInfo.CallSiteInvokeSetFuncType);
        tIlGen.Emit(OpCodes.Stsfld, tSetField);
      }

      var tSubrtractAssignField = tEmitInfo.CallSiteType.GetFieldEvenIfGeneric(tEmitInfo.CallSiteSubtractAssignName);

      using (tIlGen.EmitBranchTrue(gen => gen.Emit(OpCodes.Ldsfld, tSubrtractAssignField)))
      {
        tIlGen.EmitDynamicBinaryOpBinder(CSharpBinderFlags.None, ExpressionType.SubtractAssign,
          tEmitInfo.ContextType, tEmitInfo.ResolveReturnType);
        tIlGen.EmitCallsiteCreate(tEmitInfo.CallSiteSubtractAssignFuncType);
        tIlGen.Emit(OpCodes.Stsfld, tSubrtractAssignField);
      }


      using (tIlGen.EmitBranchTrue(gen => gen.Emit(OpCodes.Ldsfld, tGetField)))
      {
        tIlGen.EmitDynamicGetBinder(CSharpBinderFlags.None, tEmitInfo.Alias ?? tEmitInfo.Name, tEmitInfo.ContextType);
        tIlGen.EmitCallsiteCreate(tEmitInfo.CallSiteInvokeGetFuncType);
        tIlGen.Emit(OpCodes.Stsfld, tGetField);
      }

      tIlGen.Emit(OpCodes.Ldsfld, tSetField);
      tIlGen.Emit(OpCodes.Ldfld, tSetField.FieldType.GetFieldEvenIfGeneric("Target"));
      tIlGen.Emit(OpCodes.Ldsfld, tSetField);
      tIlGen.Emit(OpCodes.Ldarg_0);
      tIlGen.Emit(OpCodes.Callvirt, typeof(IProxy).GetProperty(nameof(IProxy.Original)).GetGetMethod());

      tIlGen.Emit(OpCodes.Ldsfld, tSubrtractAssignField);
      tIlGen.Emit(OpCodes.Ldfld, tSubrtractAssignField.FieldType.GetFieldEvenIfGeneric("Target"));
      tIlGen.Emit(OpCodes.Ldsfld, tSubrtractAssignField);

      tIlGen.Emit(OpCodes.Ldsfld, tGetField);
      tIlGen.Emit(OpCodes.Ldfld, tGetField.FieldType.GetFieldEvenIfGeneric("Target"));
      tIlGen.Emit(OpCodes.Ldsfld, tGetField);
      tIlGen.Emit(OpCodes.Ldarg_0);
      tIlGen.Emit(OpCodes.Callvirt, typeof(IProxy).GetProperty(nameof(IProxy.Original)).GetGetMethod());

      tIlGen.EmitCallInvokeFunc(tEmitInfo.CallSiteInvokeGetFuncType);

      tIlGen.Emit(OpCodes.Ldarg_1);
      tIlGen.EmitCallInvokeFunc(tEmitInfo.CallSiteSubtractAssignFuncType);

      tIlGen.EmitCallInvokeFunc(tEmitInfo.CallSiteInvokeSetFuncType);

      tIlGen.Emit(OpCodes.Pop);
      tIlGen.Emit(OpCodes.Ret);
    }

    var tRemoveCallSiteField = tEmitInfo.CallSiteType.GetFieldEvenIfGeneric(tEmitInfo.CallSiteRemoveName);
    using (tIlGen.EmitBranchTrue(gen => gen.Emit(OpCodes.Ldsfld, tRemoveCallSiteField)))
    {
      tIlGen.EmitDynamicMethodInvokeBinder(
        CSharpBinderFlags.InvokeSpecialName | CSharpBinderFlags.ResultDiscarded,
        tEmitInfo.Alias == null ? tRemoveMethod.Name : $"remove_{tEmitInfo.Alias}",
        Enumerable.Empty<Type>(),
        tEmitInfo.ContextType,
        tRemoveMethod.GetParameters(),
        Enumerable.Repeat(default(string),
          tEmitInfo.ResolvedRemoveParamTypes.Length));
      tIlGen.EmitCallsiteCreate(tEmitInfo.CallSiteRemoveFuncType);
      tIlGen.Emit(OpCodes.Stsfld, tRemoveCallSiteField);
    }
    tIlGen.Emit(OpCodes.Ldsfld, tRemoveCallSiteField);
    tIlGen.Emit(OpCodes.Ldfld, tRemoveCallSiteField.FieldType.GetFieldEvenIfGeneric("Target"));
    tIlGen.Emit(OpCodes.Ldsfld, tRemoveCallSiteField);
    tIlGen.Emit(OpCodes.Ldarg_0);
    tIlGen.Emit(OpCodes.Callvirt, typeof(IProxy).GetProperty(nameof(IProxy.Original)).GetGetMethod());
    tIlGen.Emit(OpCodes.Ldarg_1);
    tIlGen.EmitCallInvokeFunc(tEmitInfo.CallSiteRemoveFuncType);
    tIlGen.Emit(OpCodes.Pop);
    tIlGen.Emit(OpCodes.Ret);

    tMp.SetRemoveOnMethod(tRemoveBuilder);
  }

  private void EmitAddEvent(EventBuilder tMp, MethodBuilder tAddBuilder, EventInfo info, MethodInfo tAddMethod,
    FieldInfo tGetField, FieldInfo tSetField, FieldInfo tIsEventField, EmitEventInfo tEmitInfo)
  {
    var tIlGen = tAddBuilder.GetILGenerator();

    using (tIlGen.EmitBranchTrue(gen => gen.Emit(OpCodes.Ldsfld, tIsEventField)))
    {
      tIlGen.EmitDynamicIsEventBinder(CSharpBinderFlags.None, tEmitInfo.Alias ?? tEmitInfo.Name, tEmitInfo.ContextType);
      tIlGen.EmitCallsiteCreate(tEmitInfo.CallSiteIsEventFuncType);
      tIlGen.Emit(OpCodes.Stsfld, tIsEventField);
    }

    using (tIlGen.EmitBranchTrue(
            load => load.EmitInvocation(
              target => target.EmitInvocation(
                t => t.Emit(OpCodes.Ldsfld, tIsEventField),
                i => i.Emit(OpCodes.Ldfld, tIsEventField.FieldType.GetFieldEvenIfGeneric("Target"))
              ),
              invoke => invoke.EmitCallInvokeFunc(tEmitInfo.CallSiteIsEventFuncType),
              param => param.Emit(OpCodes.Ldsfld, tIsEventField),
              param => param.EmitInvocation(
                t => t.Emit(OpCodes.Ldarg_0),
                i => i.Emit(OpCodes.Callvirt, typeof(IProxy).GetProperty(nameof(IProxy.Original)).GetGetMethod())
              )
            )
          )
        ) //if IsEvent Not True
    {
      using (tIlGen.EmitBranchTrue(gen => gen.Emit(OpCodes.Ldsfld, tSetField)))
      {
        tIlGen.EmitDynamicSetBinderDynamicParams(CSharpBinderFlags.ValueFromCompoundAssignment, tEmitInfo.Alias ?? tEmitInfo.Name,
          tEmitInfo.ContextType, typeof(Object));
        tIlGen.EmitCallsiteCreate(tEmitInfo.CallSiteInvokeSetFuncType);
        tIlGen.Emit(OpCodes.Stsfld, tSetField);
      }

      var tAddAssigneField = tEmitInfo.CallSiteType.GetFieldEvenIfGeneric(tEmitInfo.CallSiteAddAssignName);

      using (tIlGen.EmitBranchTrue(gen => gen.Emit(OpCodes.Ldsfld, tAddAssigneField)))
      {
        tIlGen.EmitDynamicBinaryOpBinder(CSharpBinderFlags.None, ExpressionType.AddAssign, tEmitInfo.ContextType,
          tEmitInfo.ResolveReturnType);
        tIlGen.EmitCallsiteCreate(tEmitInfo.CallSiteAddAssignFuncType);
        tIlGen.Emit(OpCodes.Stsfld, tAddAssigneField);
      }

      using (tIlGen.EmitBranchTrue(gen => gen.Emit(OpCodes.Ldsfld, tGetField)))
      {
        tIlGen.EmitDynamicGetBinder(CSharpBinderFlags.None, tEmitInfo.Alias ?? tEmitInfo.Name, tEmitInfo.ContextType);
        tIlGen.EmitCallsiteCreate(tEmitInfo.CallSiteInvokeGetFuncType);
        tIlGen.Emit(OpCodes.Stsfld, tGetField);
      }


      tIlGen.Emit(OpCodes.Ldsfld, tSetField);
      tIlGen.Emit(OpCodes.Ldfld, tSetField.FieldType.GetFieldEvenIfGeneric("Target"));
      tIlGen.Emit(OpCodes.Ldsfld, tSetField);
      tIlGen.Emit(OpCodes.Ldarg_0);
      tIlGen.Emit(OpCodes.Callvirt, typeof(IProxy).GetProperty(nameof(IProxy.Original)).GetGetMethod());

      tIlGen.Emit(OpCodes.Ldsfld, tAddAssigneField);
      tIlGen.Emit(OpCodes.Ldfld, tAddAssigneField.FieldType.GetFieldEvenIfGeneric("Target"));
      tIlGen.Emit(OpCodes.Ldsfld, tAddAssigneField);

      tIlGen.Emit(OpCodes.Ldsfld, tGetField);
      tIlGen.Emit(OpCodes.Ldfld, tGetField.FieldType.GetFieldEvenIfGeneric("Target"));
      tIlGen.Emit(OpCodes.Ldsfld, tGetField);
      tIlGen.Emit(OpCodes.Ldarg_0);
      tIlGen.Emit(OpCodes.Callvirt, typeof(IProxy).GetProperty(nameof(IProxy.Original)).GetGetMethod());

      tIlGen.EmitCallInvokeFunc(tEmitInfo.CallSiteInvokeGetFuncType);

      tIlGen.Emit(OpCodes.Ldarg_1);
      tIlGen.EmitCallInvokeFunc(tEmitInfo.CallSiteAddAssignFuncType);

      tIlGen.EmitCallInvokeFunc(tEmitInfo.CallSiteInvokeSetFuncType);
      tIlGen.Emit(OpCodes.Pop);
      tIlGen.Emit(OpCodes.Ret);
    }

    var tAddCallSiteField = tEmitInfo.CallSiteType.GetFieldEvenIfGeneric(tEmitInfo.CallSiteAddName);

    using (tIlGen.EmitBranchTrue(gen => gen.Emit(OpCodes.Ldsfld, tAddCallSiteField)))
    {
      tIlGen.EmitDynamicMethodInvokeBinder(
        CSharpBinderFlags.InvokeSpecialName | CSharpBinderFlags.ResultDiscarded,
        tEmitInfo.Alias == null ? tAddMethod.Name : "add_" + tEmitInfo.Alias,
        Enumerable.Empty<Type>(),
        tEmitInfo.ContextType,
        tAddMethod.GetParameters(),
        Enumerable.Repeat(default(string),
          tEmitInfo.ResolvedAddParamTypes.Length));
      tIlGen.EmitCallsiteCreate(tEmitInfo.CallSiteAddFuncType);
      tIlGen.Emit(OpCodes.Stsfld, tAddCallSiteField);
    }
    tIlGen.Emit(OpCodes.Ldsfld, tAddCallSiteField);
    tIlGen.Emit(OpCodes.Ldfld, tAddCallSiteField.FieldType.GetFieldEvenIfGeneric("Target"));
    tIlGen.Emit(OpCodes.Ldsfld, tAddCallSiteField);
    tIlGen.Emit(OpCodes.Ldarg_0);
    tIlGen.Emit(OpCodes.Callvirt, typeof(IProxy).GetProperty(nameof(IProxy.Original)).GetGetMethod());
    for (var i = 1; i <= tEmitInfo.ResolvedAddParamTypes.Length; i++)
    {
      tIlGen.EmitLoadArgument(i);
    }
    tIlGen.EmitCallInvokeFunc(tEmitInfo.CallSiteAddFuncType);
    tIlGen.Emit(OpCodes.Pop);
    tIlGen.Emit(OpCodes.Ret);

    tMp.SetAddOnMethod(tAddBuilder);

  }


  /// <summary>
  /// Makes the property helper.
  /// </summary>
  /// <param name="builder">The builder.</param>
  /// <param name="typeBuilder">The type builder.</param>
  /// <param name="info">The info.</param>
  /// <param name="emitInfo">The emit info.</param>
  private void MakePropertyHelper(ModuleBuilder builder, TypeBuilder typeBuilder, PropertyEmitInfo emitInfo, PropertyInfo info = null)
  {


    emitInfo.ResolvedIndexParamTypes = new Type[] { };
    if (info != null)
    {
      emitInfo.ResolvedIndexParamTypes = Enumerable.Select(info.GetIndexParameters(), it => it.ParameterType).ToArray();
    }


    var tCallSiteInvokeName = emitInfo.CallSiteName;
    var tCStp = DefineBuilderForCallSite(builder, tCallSiteInvokeName);


    var tConvertGet = emitInfo.CallSiteConvertName;

    emitInfo.CallSiteConvertFuncType = DefineCallsiteField(tCStp, this, tConvertGet, emitInfo.ResolveReturnType);

    var tInvokeGet = emitInfo.CallSiteInvokeGetName;
    if (info == null || info.CanRead)
    {
      emitInfo.CallSiteInvokeGetFuncType = DefineCallsiteField(tCStp, this, tInvokeGet, typeof(object), emitInfo.ResolvedIndexParamTypes);
    }

    var tInvokeSet = emitInfo.CallSiteInvokeSetName;
    if (info != null && info.CanWrite)
    {
      emitInfo.ResolvedParamTypes = info.GetSetMethod().GetParameters().Select(it => it.ParameterType).ToArray();

      emitInfo.CallSiteInvokeSetFuncType = DefineCallsiteField(tCStp, this, tInvokeSet, typeof(object), emitInfo.ResolvedParamTypes);
    }

    emitInfo.CallSiteType = tCStp.CreateTypeInfo();



    var tPublicPrivate = MethodAttributes.Public;
    var tPrefixedGet = emitInfo.GetName;
    var tPrefixedSet = emitInfo.SetName;
    var tPrefixedName = emitInfo.Name;
    if (!emitInfo.DefaultInterfaceImplementation)
    {
      tPublicPrivate = MethodAttributes.Private;
      tPrefixedGet = $"{info.DeclaringType.FullName}.{tPrefixedGet}";
      tPrefixedSet = $"{info.DeclaringType.FullName}.{tPrefixedSet}";
      tPrefixedName = $"{info.DeclaringType.FullName}.{tPrefixedName}";
    }


    var tMp = typeBuilder.DefineProperty(tPrefixedName, PropertyAttributes.None,

      CallingConventions.HasThis,

      emitInfo.ResolveReturnType, emitInfo.ResolvedIndexParamTypes);


    MethodBuilder tGetMethodBuilder = null;
    if (info == null || info.CanRead) // q: some props just don't have GET
    {
      //GetMethod
      tGetMethodBuilder = typeBuilder.DefineMethod(tPrefixedGet,
        tPublicPrivate
        | MethodAttributes.SpecialName
        | MethodAttributes.HideBySig
        | MethodAttributes.Virtual
        | MethodAttributes.Final
        | MethodAttributes.NewSlot,
        emitInfo.ResolveReturnType,
        emitInfo.ResolvedIndexParamTypes);


      if (!emitInfo.DefaultInterfaceImplementation)
      {
        typeBuilder.DefineMethodOverride(tGetMethodBuilder, info.GetGetMethod());
      }


      if (info != null && info.GetGetMethod() != null)
      {
        foreach (var tParam in info.GetGetMethod().GetParameters())
        {
          tGetMethodBuilder.DefineParameter(tParam.Position + 1, AttributesForParam(tParam), tParam.Name);
        }
      }
    }

    MethodBuilder tSetMethodBuilder = null;
    if (info != null && info.CanWrite) // q: some props just don't have SET
    {
      //SetMethod
      tSetMethodBuilder = typeBuilder.DefineMethod(tPrefixedSet,
        tPublicPrivate
        | MethodAttributes.SpecialName
        | MethodAttributes.HideBySig
        | MethodAttributes.Virtual
        | MethodAttributes.Final
        | MethodAttributes.NewSlot,
        null,
        emitInfo.ResolvedParamTypes);

      if (!emitInfo.DefaultInterfaceImplementation)
      {
        typeBuilder.DefineMethodOverride(tSetMethodBuilder, info.GetSetMethod());
      }

      foreach (var tParam in info.GetSetMethod().GetParameters())
      {
        tSetMethodBuilder.DefineParameter(tParam.Position + 1, AttributesForParam(tParam), tParam.Name);
      }
    }

    EmitProperty(
      typeBuilder,
      tGetMethodBuilder,
      tMp,
      info,
      tSetMethodBuilder,
      emitInfo);
  }
  private abstract class EmitInfo
  {
    public string Name { get; set; }
    public string Alias { get; set; }

    protected EmitInfo()
    {
      _callSiteName =
        new Lazy<string>(() => $"Callsite_{Name}_{Guid.NewGuid():N}");
    }

    private readonly Lazy<string> _callSiteName;
    public string CallSiteName
    {
      get { return _callSiteName.Value; }
    }
    public bool NonRecursive { get; set; }
    public bool DefaultInterfaceImplementation { get; set; }
    public IEnumerable<string> ArgNames { get; set; }
    public Type[] ResolvedParamTypes { get; set; }
    public Type ResolveReturnType { get; set; }
    public Type CallSiteType { get; set; }
    public Type ContextType { get; set; }

  }

  private class MethodEmitInfo : EmitInfo
  {
    public MethodEmitInfo()
    {
      CallSiteInvokeName = "Invoke_Method";
      CallSiteConvertName = "Convert_Method";
    }

    public string CallSiteInvokeName { get; protected set; }
    public string CallSiteConvertName { get; protected set; }

    public Type CallSiteConvertFuncType { get; set; }
    public Type CallSiteInvokeFuncType { get; set; }
  }

  public static CustomAttributeBuilder ToCustomAttributeBuilder(CustomAttributeData customAttributeData)
  {
    return new CustomAttributeBuilder(
      customAttributeData.Constructor,
      customAttributeData.ConstructorArguments
        .Select(
          arg =>
            arg.Value?.GetType()
            == typeof(ReadOnlyCollection<CustomAttributeTypedArgument>)
              ? ((ReadOnlyCollection<CustomAttributeTypedArgument>)arg.Value)
              .Select(arrayArg => arrayArg.Value)
              .ToArray()
              : arg.Value
        )
        .ToArray(),
      customAttributeData.NamedArguments
        .Where(arg => arg.MemberInfo.MemberType == MemberTypes.Property)
        .Select(arg => arg.MemberInfo)
        .Cast<PropertyInfo>()
        .ToArray(),
      customAttributeData.NamedArguments
        .Where(arg => arg.MemberInfo.MemberType == MemberTypes.Property)
        .Select(arg => arg.TypedValue.Value)
        .ToArray(),
      customAttributeData.NamedArguments
        .Where(arg => arg.MemberInfo.MemberType == MemberTypes.Field)
        .Select(arg => arg.MemberInfo)
        .Cast<FieldInfo>()
        .ToArray(),
      customAttributeData.NamedArguments
        .Where(arg => arg.MemberInfo.MemberType == MemberTypes.Field)
        .Select(arg => arg.TypedValue.Value)
        .ToArray()
    );
  }

  private void EmitProperty(
    TypeBuilder typeBuilder,
    MethodBuilder getMethodBuilder,
    PropertyBuilder tMp,
    PropertyInfo info,
    MethodBuilder setMethodBuilder,
    PropertyEmitInfo emitInfo
  )
  {
    if (emitInfo.ResolvedIndexParamTypes == null) throw new ArgumentNullException(nameof(emitInfo), "ResolvedIndexParams can't be null");

    if (getMethodBuilder != null)
    {
      var tIlGen = getMethodBuilder.GetILGenerator();
      var tConvertCallsiteField = emitInfo.CallSiteType.GetFieldEvenIfGeneric(emitInfo.CallSiteConvertName);

      //If it's an interface and not nonrecursive this will be true
      var tRecurse = emitInfo.ResolveReturnType.IsInterface && !emitInfo.NonRecursive;

      //If we want to do recursive Interfaces we need to test the interface before we dynamically cast
      var tReturnLocal = tIlGen.DeclareLocal(tRecurse ? typeof(object) : emitInfo.ResolveReturnType);

      using (tIlGen.EmitBranchTrue(gen => gen.Emit(OpCodes.Ldsfld, tConvertCallsiteField)))
      {
        tIlGen.EmitDynamicConvertBinder(CSharpBinderFlags.None, emitInfo.ResolveReturnType, emitInfo.ContextType);
        tIlGen.EmitCallsiteCreate(emitInfo.CallSiteConvertFuncType);
        tIlGen.Emit(OpCodes.Stsfld, tConvertCallsiteField);
      }

      var tInvokeGetCallsiteField = emitInfo.CallSiteType.GetFieldEvenIfGeneric(emitInfo.CallSiteInvokeGetName);

      using (tIlGen.EmitBranchTrue(gen => gen.Emit(OpCodes.Ldsfld, tInvokeGetCallsiteField)))
      {
        tIlGen.EmitDynamicGetBinder(CSharpBinderFlags.None, emitInfo.Alias ?? emitInfo.Name, emitInfo.ContextType, emitInfo.ResolvedIndexParamTypes);
        tIlGen.EmitCallsiteCreate(emitInfo.CallSiteInvokeGetFuncType);
        tIlGen.Emit(OpCodes.Stsfld, tInvokeGetCallsiteField);
      }

      if (!tRecurse)
      {
        tIlGen.Emit(OpCodes.Ldsfld, tConvertCallsiteField);
        tIlGen.Emit(OpCodes.Ldfld, tConvertCallsiteField.FieldType.GetFieldEvenIfGeneric("Target"));
        tIlGen.Emit(OpCodes.Ldsfld, tConvertCallsiteField);
      }
      tIlGen.Emit(OpCodes.Ldsfld, tInvokeGetCallsiteField);
      tIlGen.Emit(OpCodes.Ldfld, tInvokeGetCallsiteField.FieldType.GetFieldEvenIfGeneric("Target"));
      tIlGen.Emit(OpCodes.Ldsfld, tInvokeGetCallsiteField);
      tIlGen.Emit(OpCodes.Ldarg_0);
      tIlGen.Emit(OpCodes.Callvirt, typeof(IProxy).GetProperty(nameof(IProxy.Original)).GetGetMethod());
      for (var i = 1; i <= emitInfo.ResolvedIndexParamTypes.Length; i++)
      {
        tIlGen.EmitLoadArgument(i);
      }
      tIlGen.EmitCallInvokeFunc(emitInfo.CallSiteInvokeGetFuncType);

      //If we are are recursing than do it later
      if (!tRecurse)
      {
        tIlGen.EmitCallInvokeFunc(emitInfo.CallSiteConvertFuncType);
      }

      tIlGen.EmitStoreLocation(tReturnLocal.LocalIndex);

      //If return type is interface and instance is not interface, try actlike
      if (tRecurse)
      {
        using (tIlGen.EmitBranchFalse(gen => gen.EmitLoadLocation(tReturnLocal.LocalIndex)))
        {
          tIlGen.EmitLoadLocation(tReturnLocal.LocalIndex);
          using (tIlGen.EmitBranchTrue(gen => gen.Emit(OpCodes.Isinst, emitInfo.ResolveReturnType)))
          {
            tIlGen.Emit(OpCodes.Ldarg_0);
            tIlGen.Emit(OpCodes.Callvirt, typeof(IProxy).GetProperty(nameof(IProxy.Maker)).GetGetMethod());
            tIlGen.EmitLoadLocation(tReturnLocal.LocalIndex);
            tIlGen.Emit(OpCodes.Call, ActLikeRec.MakeGenericMethod(emitInfo.ResolveReturnType));
            tIlGen.Emit(OpCodes.Ret);
          }
        }
      }

      var tReturnLabel = tIlGen.DefineLabel();
      tIlGen.Emit(OpCodes.Br_S, tReturnLabel);
      tIlGen.MarkLabel(tReturnLabel);
      if (tRecurse)
      {
        tIlGen.Emit(OpCodes.Ldsfld, tConvertCallsiteField);
        tIlGen.Emit(OpCodes.Ldfld, tConvertCallsiteField.FieldType.GetFieldEvenIfGeneric("Target"));
        tIlGen.Emit(OpCodes.Ldsfld, tConvertCallsiteField);
      }
      tIlGen.EmitLoadLocation(tReturnLocal.LocalIndex);
      if (tRecurse)
      {
        tIlGen.EmitCallInvokeFunc(emitInfo.CallSiteConvertFuncType);
      }

      tIlGen.Emit(OpCodes.Ret);
      tMp.SetGetMethod(getMethodBuilder);
    }

    if (setMethodBuilder != null)
    {
      var tIlGen = setMethodBuilder.GetILGenerator();
      var tSetCallsiteField = emitInfo.CallSiteType.GetFieldEvenIfGeneric(emitInfo.CallSiteInvokeSetName);

      using (tIlGen.EmitBranchTrue(gen => gen.Emit(OpCodes.Ldsfld, tSetCallsiteField)))
      {
        tIlGen.EmitDynamicSetBinder(CSharpBinderFlags.None, emitInfo.Alias ?? emitInfo.Name, emitInfo.ContextType, emitInfo.ResolvedParamTypes);
        tIlGen.EmitCallsiteCreate(emitInfo.CallSiteInvokeSetFuncType);
        tIlGen.Emit(OpCodes.Stsfld, tSetCallsiteField);
      }
      tIlGen.Emit(OpCodes.Ldsfld, tSetCallsiteField);
      tIlGen.Emit(OpCodes.Ldfld, tSetCallsiteField.FieldType.GetFieldEvenIfGeneric("Target"));
      tIlGen.Emit(OpCodes.Ldsfld, tSetCallsiteField);
      tIlGen.Emit(OpCodes.Ldarg_0);
      tIlGen.Emit(OpCodes.Callvirt, typeof(IProxy).GetProperty(nameof(IProxy.Original)).GetGetMethod());
      for (var i = 1; i <= emitInfo.ResolvedParamTypes.Length; i++)
      {
        tIlGen.EmitLoadArgument(i);
      }
      tIlGen.EmitCallInvokeFunc(emitInfo.CallSiteInvokeSetFuncType);
      tIlGen.Emit(OpCodes.Pop);
      tIlGen.Emit(OpCodes.Ret);
      tMp.SetSetMethod(setMethodBuilder);
    }

    if (info != null)
    {

      foreach (var attrData in info.GetCustomAttributesData())
      {
        tMp.SetCustomAttribute(ToCustomAttributeBuilder(attrData));
      }
    }
  }

  private Type UpdateCallsiteFuncType(Type tFuncGeneric, Type returnType, params Type[] argTypes)
  {
    var tList = new List<Type> { typeof(CallSite), typeof(object) };
    tList.AddRange(argTypes);
    if (returnType != typeof(void))
      tList.Add(returnType);

    IEnumerable<Type> tTypeArguments = tList;

    var tDef = tFuncGeneric.GetGenericTypeDefinition();

    if (tDef.GetGenericArguments().Count() != tTypeArguments.Count())
    {
      tTypeArguments = tTypeArguments.Where(it => it.IsGenericParameter);
    }

    var tFuncType = tDef.MakeGenericType(tTypeArguments.ToArray());

    return tFuncType;
  }

  private Type ReduceToElementType(Type type)
  {
    if (type.IsByRef || type.IsPointer || type.IsArray)
      return type.GetElementType();
    return type;
  }

  /// <summary>
  /// Emits new delegate type of the call site func.
  /// </summary>
  /// <param name="argTypes">The arg types.</param>
  /// <param name="returnType">Type of the return.</param>
  /// <returns></returns>
  public Type EmitCallSiteFuncType(IEnumerable<Type> argTypes, Type returnType)
  {
    return GenerateCallSiteFuncType(argTypes, returnType);
  }

  /// <summary>
  /// Generates the delegate type of the call site function.
  /// </summary>
  /// <param name="argTypes">The arg types.</param>
  /// <param name="returnType">Type of the return.</param>
  /// <param name="methodInfo">The method info. Required for reference types or delegates with more than 16 arguments.</param>
  /// <param name="builder">The Type Builder. Required for reference types or delegates with more than 16 arguments.</param>
  /// <returns></returns>
  internal Type GenerateCallSiteFuncType(IEnumerable<Type> argTypes, Type returnType, MethodInfo methodInfo = null, TypeBuilder builder = null)
  {
    bool tIsFunc = returnType != typeof(void);

    var tList = new List<Type> { typeof(CallSite), typeof(object) };
    tList.AddRange(argTypes.Select(it => (it.IsNotPublic && !it.IsByRef) ? typeof(object) : it));

    lock (DelegateCacheLock)
    {
      TypeHash tHash;

      if ((tList.Any(it => it.IsByRef) || tList.Count > 16) && methodInfo != null)
      {
        tHash = TypeHash.Create(strictOrder: true, moreTypes: methodInfo);
      }
      else
      {
        tHash = TypeHash.Create(strictOrder: true, moreTypes: tList.Concat(new[] { returnType }).ToArray());
      }

      if (_delegateCache.TryGetValue(tHash, out var tType))
      {
        return tType;
      }

      if (tList.Any(it => it.IsByRef)
        || (tIsFunc && tList.Count >= 16)
        || (!tIsFunc && tList.Count >= 16))
      {
        tType = DynamicTypeBuilder.GenerateFullDelegate(builder, returnType, tList, methodInfo);

        _delegateCache[tHash] = tType;
        return tType;
      }

      if (tIsFunc)
        tList.Add(returnType);

      var tFuncGeneric = Dynamitey.Dynamic.GenericDelegateType(tList.Count, !tIsFunc);

      var tFuncType = tFuncGeneric.MakeGenericType(tList.ToArray());

      _delegateCache[tHash] = tFuncType;

      return tFuncType;
    }
  }

  internal ParameterAttributes AttributesForParam(ParameterInfo param)
  {
    return param.Attributes;
  }
}
