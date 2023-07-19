open System.Threading.Tasks

// Throws exception:
(*
Unhandled exception. System.AggregateException: One or more errors occurred. (Object reference not set to an instance of an object.)
 ---> System.NullReferenceException: Object reference not set to an instance of an object.
   at Microsoft.FSharp.Control.TaskBuilderExtensions.HighPriority.TaskBuilderBase.BindDynamic.Static[TOverall,TResult1,TResult2](ResumableStateMachine`1& sm, Task`1 task
, FSharpFunc`2 continuation) in D:\a\_work\1\s\src\FSharp.Core\tasks.fs:line 413
   at Program.repro@4.MoveNext() in C:\dev\Repro\task-bug-repro\task-bug-repro\Program.fs:line 10
   --- End of inner exception stack trace ---
   at System.Threading.Tasks.Task.WaitAllCore(Task[] tasks, Int32 millisecondsTimeout, CancellationToken cancellationToken)
   at System.Threading.Tasks.Task.WaitAll(Task[] tasks)
   at <StartupCode$task-bug-repro>.$Program.main@() in C:\dev\Repro\task-bug-repro\task-bug-repro\Program.fs:line 28
*)
let repro () =
    task {
        //let hof (f: _ -> Task<_>) = task { // this fixes it
        let hof f = task { return! f () }
        let! _ = async { return () }
        printfn $"Task completed (line {__LINE__})"
        return ()
    }

repro () |> Task.WaitAll

// internal error: The local field ResumptionDynamicInfo was referenced but not declared
let internalError () =
    task {
        //let hof (f: _ -> Task<_>) = task { // this fixes it
        let hof f = task { return! f () }
        // uncomment to get internal error: The local field ResumptionDynamicInfo was referenced but not declared
        // let! _ = Task.Delay 1000
        printfn $"Task completed (line {__LINE__})"
        return ()
    }
