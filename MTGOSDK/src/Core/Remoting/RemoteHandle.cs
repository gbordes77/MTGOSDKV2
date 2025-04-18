/** @file
  Copyright (c) 2021, Xappy.
  Copyright (c) 2024, Cory Bennett. All rights reserved.
  SPDX-License-Identifier: Apache-2.0
**/

using System.Diagnostics;
using System.IO;
using System.Threading;

using MTGOSDK.Core.Logging;
using MTGOSDK.Core.Reflection;
using MTGOSDK.Core.Remoting.Interop;
using MTGOSDK.Core.Remoting.Interop.Interactions.Dumps;
using MTGOSDK.Core.Remoting.Types;
using MTGOSDK.Core.Remoting.Structs;

using MTGOSDK.Resources;


namespace MTGOSDK.Core.Remoting;

public class RemoteHandle : DLRWrapper, IDisposable
{
  internal class RemoteObjectsCollection
  {
    // The WeakReferences are to RemoteObject
    private readonly Dictionary<ulong, WeakReference<RemoteObject>> _pinnedAddressesToRemoteObjects;
    private readonly ReaderWriterLockSlim _rwLock = new ReaderWriterLockSlim();

    private readonly RemoteHandle _app;

    public RemoteObjectsCollection(RemoteHandle app)
    {
      _app = app;
      _pinnedAddressesToRemoteObjects = new Dictionary<ulong, WeakReference<RemoteObject>>();
    }

    private RemoteObject GetRemoteObjectUncached(
      ulong remoteAddress,
      string typeName,
      int? hashCode = null)
    {
      ObjectDump od = null!;
      TypeDump td = null!;
      Retry(() =>
      {
        try
        {
          od = _app.Communicator.DumpObject(remoteAddress, typeName, true, hashCode);
          td = _app.Communicator.DumpType(od.Type);
        }
        catch (Exception e)
        {
          throw new Exception("Could not dump remote object/type.", e);
        }
      }, raise: true);

      var objRef = new RemoteObjectRef(od, td, _app.Communicator);
      var remoteObject = new RemoteObject(objRef, _app);

      return remoteObject;
    }

    public RemoteObject GetRemoteObject(
      ulong address,
      string typeName,
      int? hashcode = null)
    {
      RemoteObject ro;
      WeakReference<RemoteObject> weakRef;

      _rwLock.EnterUpgradeableReadLock();
      try
      {
        // Check cache under upgradeable read lock
        if (_pinnedAddressesToRemoteObjects.TryGetValue(address, out weakRef))
        {
          if (weakRef.TryGetTarget(out ro) && ro.IsValid)
          {
            // Tentatively valid, try to secure reference
            ro.AddReference();
            if (ro.IsValid)
            {
              // Still valid after adding reference, return it
              return ro;
            }
            else
            {
              // Became invalid between IsValid check and AddReference/second IsValid check.
              // Need to remove the stale entry.
              _rwLock.EnterWriteLock();
              try
              {
                // Re-check if the entry is still the same weakRef before removing
                if (_pinnedAddressesToRemoteObjects.TryGetValue(address, out var currentWeakRef) && currentWeakRef == weakRef)
                {
                  _pinnedAddressesToRemoteObjects.Remove(address);
                }
                // If it changed, another thread already handled it, so we do nothing here.
              }
              finally
              {
                _rwLock.ExitWriteLock();
              }
              // Fall through to fetch uncached version
            }
          }
          else
          {
            // Object was GC'd or initially invalid. Need write lock to remove.
            _rwLock.EnterWriteLock();
            try
            {
              // Re-check if the entry is still the same weakRef before removing
              if (_pinnedAddressesToRemoteObjects.TryGetValue(address, out var currentWeakRef) && currentWeakRef == weakRef)
              {
                _pinnedAddressesToRemoteObjects.Remove(address);
              }
              // If it changed, another thread already handled it, so we do nothing here.
            }
            finally
            {
              _rwLock.ExitWriteLock();
            }
            // Fall through to fetch uncached version
          }
        }

        // Object not in cache or was removed. Need write lock to add.
        _rwLock.EnterWriteLock();
        try
        {
          // Double-check: Another thread might have added it while waiting for the write lock.
          if (_pinnedAddressesToRemoteObjects.TryGetValue(address, out weakRef))
          {
            if (weakRef.TryGetTarget(out ro) && ro.IsValid)
            {
              ro.AddReference();
              if (ro.IsValid)
              {
                // Added by another thread and still valid.
                return ro;
              }
              else
              {
                // Added by another thread but became invalid quickly. Remove it again.
                _pinnedAddressesToRemoteObjects.Remove(address);
                // Fall through to fetch uncached version outside the write lock (but still in upgradeable read)
              }
            }
            else
            {
              // Added by another thread but already GC'd or invalid. Remove it.
              _pinnedAddressesToRemoteObjects.Remove(address);
              // Fall through to fetch uncached version outside the write lock (but still in upgradeable read)
            }
          }

          // Get remote (object not in cache or was invalid/removed)
          ro = GetRemoteObjectUncached(address, typeName, hashcode);

          // Add to cache
          weakRef = new WeakReference<RemoteObject>(ro);
          _pinnedAddressesToRemoteObjects.Add(ro.RemoteToken, weakRef); // Add under write lock
          return ro; // Return the newly created object
        }
        finally
        {
          _rwLock.ExitWriteLock();
        }
      }
      finally
      {
        _rwLock.ExitUpgradeableReadLock();
      }
    }
  }

  private Process _procWithDiver;
  private readonly DomainDump _currentDomain;
  private readonly Dictionary<string, TypesDump> _remoteTypes = new();
  private readonly RemoteObjectsCollection _remoteObjects;

  public Process Process => _procWithDiver;
  public RemoteActivator Activator { get; private set; }
  public RemoteHarmony Harmony { get; private set; }

  private static DiverCommunicator _communicator;
  public DiverCommunicator Communicator => _communicator;
  public static bool IsReconnected = false;

  internal RemoteHandle(Process procWithDiver, DiverCommunicator communicator)
  {
    _procWithDiver = procWithDiver;
    _communicator = communicator;

    _currentDomain = communicator.DumpDomain();
    foreach (string assembly in _currentDomain.Modules)
    {
      try
      {
        if (_remoteTypes.ContainsKey(assembly)) continue;
        _remoteTypes.Add(assembly, communicator.DumpTypes(assembly));
      }
      catch (Exception e)
      {
        Log.Error("Failed to dump types for assembly '{Assembly}': {Message}",
          assembly, e.Message);
      }
    }
    _remoteObjects = new RemoteObjectsCollection(this);
    Activator = new RemoteActivator(communicator, this);
    Harmony = new RemoteHarmony(this);
  }

  //
  // Init
  //

  /// <summary>
  /// Creates a new provider.
  /// </summary>
  /// <param name="target">Process to create the provider for</param>
  /// <returns>A provider for the given process</returns>
  public static RemoteHandle Connect(Process target)
  {
    return Connect(target, (ushort)target.Id);
  }

  public static RemoteHandle Connect(
    Process target,
    ushort diverPort,
    CancellationTokenSource? cts = null)
  {
    // Use discovery to check for existing diver
    string diverAddr = "127.0.0.1";
    switch(Bootstrapper.QueryStatus(target, diverAddr, diverPort))
    {
      case DiverState.NoDiver:
        // No diver, we need to inject one
        try
        {
          Bootstrapper.Inject(target, diverPort);
          break;
        }
        catch (IOException e)
        {
          throw new Exception("Failed to inject diver.", e);
        }
      case DiverState.Alive:
        // Skip injection as diver assembly is already bootstrapped
        IsReconnected = true;
        break;
      case DiverState.Corpse:
        throw new Exception("Diver could not finish bootstrapping.");
      case DiverState.HollowSnapshot:
        throw new Exception("Target process is empty. Did you attach to the correct process?");
    }

    // Now register our program as a "client" of the diver
    DiverCommunicator com = new DiverCommunicator(diverAddr, diverPort, cts);
    if (com.RegisterClient() == false)
      throw new Exception("Registering as a client in the Diver failed.");

    return new RemoteHandle(target, com);
  }

  //
  // Remote Heap querying
  //

  public IEnumerable<CandidateType> QueryTypes(string typeFullName)
  {
    foreach (string assembly in _currentDomain.Modules)
    {
      if (!_remoteTypes.ContainsKey(assembly))
        continue;

      foreach (TypesDump.TypeIdentifiers type in _remoteTypes[assembly].Types)
      {
        if (type.TypeName == typeFullName)
          yield return new CandidateType(type.TypeName, assembly);
      }
    }
  }

  public IEnumerable<CandidateObject> QueryInstances(Type typeFilter, bool dumpHashcodes = true) =>
    QueryInstances(typeFilter.FullName, dumpHashcodes);

  /// <summary>
  /// Gets all object candidates for a specific filter
  /// </summary>
  /// <param name="typeFullNameFilter">Objects with Full Type Names of this EXACT string will be returned. You can use '*' as a "0 or more characters" wildcard</param>
  /// <param name="dumpHashcodes">Whether to also dump hashcodes of every matching object.
  /// This makes resolving the candidates later more reliable but for wide queries (e.g. "*") this might fail the entire search since it causes instabilities in the heap when examining it.
  /// </param>
  public IEnumerable<CandidateObject> QueryInstances(string typeFullNameFilter, bool dumpHashcodes = true)
  {
    return Communicator.DumpHeap(typeFullNameFilter, dumpHashcodes).Objects
      .Select(heapObj =>
        new CandidateObject(heapObj.Address, heapObj.Type, heapObj.HashCode));
  }

  //
  // Resolving Types
  //

  /// <summary>
  /// Gets a handle to a remote type (even ones from assemblies we aren't
  /// referencing/loading to the local process)
  /// </summary>
  /// <param name="typeFullName">Full name of the type to get. For example 'System.Xml.XmlDocument'</param>
  /// <param name="assembly">Optional short name of the assembly containing the type. For example 'System.Xml.ReaderWriter.dll'</param>
  /// <returns></returns>
  public Type GetRemoteType(string typeFullName, string assembly = null)
  {
    // Easy case: Trying to resolve from cache or from local assemblies
    var resolver = TypeResolver.Instance;
    Type res = resolver.Resolve(assembly, typeFullName);
    if (res != null)
    {
      // Either found in cache or found locally.

      // If it's a local type we need to wrap it in a "fake" RemoteType (So
      // method invocations will actually happened in the remote app, for
      // example) (But not for primitives...)
      if (!(res is RemoteType) && !res.IsPrimitive)
      {
        res = new RemoteType(this, res);
        // TODO: Registering here in the cache is a hack but we couldn't
        // register within "TypeResolver.Resolve" because we don't have the
        // RemoteHandle to associate the fake remote type with.
        // Maybe this should move somewhere else...
        resolver.RegisterType(res);
      }

      return res;
    }

    // Harder case: Dump the remote type. This takes much more time (includes
    // dumping of dependent types) and should be avoided as much as possible.
    RemoteTypesFactory rtf = new RemoteTypesFactory(resolver, Communicator);
    var dumpedType = Communicator.DumpType(typeFullName, assembly);
    return rtf.Create(this, dumpedType);
  }

  /// <summary>
  /// Returns a handle to a remote type based on a given local type.
  /// </summary>
  public Type GetRemoteType(Type localType) =>
    GetRemoteType(localType.FullName, localType.Assembly.GetName().Name);
  public Type GetRemoteType(CandidateType candidate) =>
    GetRemoteType(candidate.TypeFullName, candidate.Assembly);
  internal Type GetRemoteType(TypeDump typeDump) =>
    GetRemoteType(typeDump.Type, typeDump.Assembly);

  public RemoteEnum GetRemoteEnum(string typeFullName, string assembly = null)
  {
    RemoteType remoteType = GetRemoteType(typeFullName, assembly) as RemoteType
      ?? throw new Exception("Failed to dump remote enum (and get a RemoteType object)");
    return new RemoteEnum(remoteType);
  }

  //
  // Getting Remote Objects
  //

  public RemoteObject GetRemoteObject(CandidateObject candidate) =>
    GetRemoteObject(candidate.Address, candidate.TypeFullName, candidate.HashCode);

  public RemoteObject GetRemoteObject(
    ulong remoteAddress,
    string typeName,
    int? hashCode = null)
  {
    return _remoteObjects.GetRemoteObject(remoteAddress, typeName, hashCode);
  }

  //
  // IDisposable
  //
  public void Dispose()
  {
    _communicator?.Disconnect();
    _communicator = null;
    _procWithDiver = null;

    // Clear global type cache
    TypeResolver.Instance.ClearCache();
    _remoteTypes.Clear();
  }
}
