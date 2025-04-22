/** @file
  Copyright (c) 2024, Cory Bennett. All rights reserved.
  SPDX-License-Identifier: Apache-2.0
**/

using System;
using System.Threading;
using System.Threading.Tasks;

using NUnit.Framework.Internal;
using NUnit.Framework.Internal.Commands;


namespace MTGOSDK.NUnit.Attributes;

public class STACommand(TestCommand command) : TestCommand(command.Test)
{
  public virtual TestResult RunCommand(TestExecutionContext context)
  {
    return command.Execute(context);
  }

  public override TestResult Execute(TestExecutionContext context)
  {
    var tcs = new TaskCompletionSource<TestResult>();
    var thread = new Thread(() =>
    {
      try
      {
        tcs.SetResult(RunCommand(context));
      }
      catch (Exception e)
      {
        tcs.SetException(e);
        // Filter stack trace to exclude internal frames
        StackFilter.FilterException(context);
      }
    });
    thread.SetApartmentState(ApartmentState.STA);
    thread.Start();
    thread.Join();

    return tcs.Task.Result;
  }
}
