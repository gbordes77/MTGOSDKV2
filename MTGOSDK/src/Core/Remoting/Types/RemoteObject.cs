/** @file
  Copyright (c) 2021, Xappy.
  Copyright (c) 2024, Cory Bennett. All rights reserved.
  SPDX-License-Identifier: Apache-2.0
**/

using MTGOSDK.Core.Memory;
using MTGOSDK.Core.Remoting.Interop;
using MTGOSDK.Core.Remoting.Interop.Interactions;


namespace MTGOSDK.Core.Remoting.Types;

public class RemoteObject : IObjectReference
{
  public void AddReference() => _ref?.AddReference();

  public void ReleaseReference(bool useJitter = false)
  {
    if (_isDisposed) return;
    _isDisposed = true;

    _ref?.ReleaseReference(useJitter);
  }

  public bool IsValid => _ref != null && _ref.IsValid && !_isDisposed;

  private bool _isDisposed = false;

  ~RemoteObject()
  {
    if (IsValid)
    {
      GCTimer.Enqueue(this);
      GC.SuppressFinalize(this);
    }
  }

  private readonly RemoteHandle _app;
  private readonly RemoteObjectRef _ref;
  private Type _type = null;

  private readonly Dictionary<Delegate, DiverCommunicator.LocalEventCallback> _eventCallbacksAndProxies = new();

  public ulong RemoteToken => _ref.Token;

  internal RemoteObject(RemoteObjectRef reference, RemoteHandle remoteApp)
  {
    // Increment the reference count of the remote object
    reference.AddReference();

    // Create a strong reference to the remote object
    _ref = reference;
    _app = remoteApp;
  }

  /// <summary>
  /// Gets the type of the proxied remote object, in the remote app. (This does not return `typeof(RemoteObject)`)
  /// </summary>
  public new Type GetType() => _type ??= _app.GetRemoteType(_ref.GetTypeDump());

  public ObjectOrRemoteAddress SetField(string fieldName, ObjectOrRemoteAddress newValue)
  {
    InvocationResults invokeRes = _ref.SetField(fieldName, newValue);
    return invokeRes.ReturnedObjectOrAddress;

  }

  public (bool hasResults, ObjectOrRemoteAddress returnedValue) InvokeMethod(
    string methodName,
    params ObjectOrRemoteAddress[] args)
  => InvokeMethod(methodName, args);

  public (bool hasResults, ObjectOrRemoteAddress returnedValue) InvokeMethod(
    string methodName,
    string[] genericArgsFullTypeNames,
    params ObjectOrRemoteAddress[] args)
  {
    InvocationResults invokeRes = _ref.InvokeMethod(methodName, genericArgsFullTypeNames, args);
    if (invokeRes.VoidReturnType)
    {
      return (false, null);
    }
    return (true, invokeRes.ReturnedObjectOrAddress);
  }

  public dynamic Dynamify() => new DynamicRemoteObject(_app, this);

  public override string ToString()
  {
    return $"RemoteObject. Type: {_type?.FullName ?? "UNK"} Reference: [{_ref}]";
  }

  public ObjectOrRemoteAddress GetField(string name)
  {
    var res = _ref.GetField(name);
    return res.ReturnedObjectOrAddress;
  }

  public void EventSubscribe(string eventName, Delegate callback)
  {
    // TODO: Add a check for amount of parameters and types (need to be dynamics)
    // See implementation inside DynamicEventProxy

    void callbackProxy(ObjectOrRemoteAddress[] args)
    {
      DynamicRemoteObject[] droParameters = new DynamicRemoteObject[args.Length];
      for (int i = 0; i < args.Length; i++)
      {
        RemoteObject ro = _app.GetRemoteObject(args[i].RemoteAddress, args[i].Type);
        DynamicRemoteObject dro = ro.Dynamify() as DynamicRemoteObject;
        dro.__timestamp = args[i].Timestamp;

        droParameters[i] = dro;
      }

      // Call the callback with the proxied parameters (using DynamicRemoteObjects)
      callback.DynamicInvoke(droParameters);
    }

    _eventCallbacksAndProxies[callback] = callbackProxy;

    _ref.EventSubscribe(eventName, callbackProxy);
  }

  public void EventUnsubscribe(string eventName, Delegate callback)
  {
    if (_eventCallbacksAndProxies.TryGetValue(callback, out DiverCommunicator.LocalEventCallback callbackProxy))
    {
      _ref.EventUnsubscribe(eventName, callbackProxy);

      _eventCallbacksAndProxies.Remove(callback);
    }
  }

  internal ObjectOrRemoteAddress GetItem(ObjectOrRemoteAddress key)
  {
    return _ref.GetItem(key);
  }
}
