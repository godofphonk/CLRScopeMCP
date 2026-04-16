// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
//
// PATCHED VERSION for CLRScopeMCP
// Changes:
// - Removed EventPipeSessionController class (depends on internal IpcEndpointConfig, PidIpcEndpoint)
// - DumpFromEventPipe replaced with PlatformNotSupportedException (live process collection not needed)
// - DumpFromEventPipeFile kept unchanged (file-based analysis works without internal types)

using System;
using System.Collections.Generic;
using System.Diagnostics.Tracing;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Graphs;
using Microsoft.Diagnostics.NETCore.Client;
using Microsoft.Diagnostics.Tracing;
using Microsoft.Diagnostics.Tracing.Parsers;
using Microsoft.Diagnostics.Tracing.Parsers.Clr;

namespace Microsoft.Diagnostics.Tools.GCDump
{
    public static class EventPipeDotNetHeapDumper
    {
        internal static volatile bool eventPipeDataPresent;
        internal static volatile bool dumpComplete;

        /// <summary>
        /// Given a nettrace file from a EventPipe session with the appropriate provider and keywords turned on,
        /// generate a GCHeapDump using the resulting events.
        /// </summary>
        /// <param name="path"></param>
        /// <param name="memoryGraph"></param>
        /// <param name="log"></param>
        /// <param name="dotNetInfo"></param>
        /// <returns></returns>
        public static bool DumpFromEventPipeFile(string path, MemoryGraph memoryGraph, TextWriter log, DotNetHeapInfo dotNetInfo)
        {
            // PATCH: Reset static flags to avoid pollution from repeated calls in server scenario
            eventPipeDataPresent = false;
            dumpComplete = false;

            DateTime start = DateTime.Now;
            Func<TimeSpan> getElapsed = () => DateTime.Now - start;

            DotNetHeapDumpGraphReader dumper = new(log)
            {
                DotNetHeapInfo = dotNetInfo
            };

            try
            {
                TimeSpan lastEventPipeUpdate = getElapsed();

                int gcNum = -1;

                EventPipeEventSource source = new(path);

                source.Clr.GCStart += delegate (GCStartTraceData data)
                {
                    eventPipeDataPresent = true;

                    if (gcNum < 0 && data.Depth == 2 && data.Type != GCType.BackgroundGC)
                    {
                        gcNum = data.Count;
                        log.WriteLine("{0,5:n1}s: .NET Dump Started...", getElapsed().TotalSeconds);
                    }
                };

                source.Clr.GCStop += delegate (GCEndTraceData data)
                {
                    if (data.Count == gcNum)
                    {
                        log.WriteLine("{0,5:n1}s: .NET GC Complete.", getElapsed().TotalSeconds);
                        dumpComplete = true;
                    }
                };

                source.Clr.GCBulkNode += delegate (GCBulkNodeTraceData data)
                {
                    eventPipeDataPresent = true;

                    if ((getElapsed() - lastEventPipeUpdate).TotalMilliseconds > 500)
                    {
                        log.WriteLine("{0,5:n1}s: Making GC Heap Progress...", getElapsed().TotalSeconds);
                    }

                    lastEventPipeUpdate = getElapsed();
                };

                if (memoryGraph != null)
                {
                    dumper.SetupCallbacks(memoryGraph, source);
                }

                log.WriteLine("{0,5:n1}s: Starting to process events", getElapsed().TotalSeconds);
                source.Process();
                log.WriteLine("{0,5:n1}s: Finished processing events", getElapsed().TotalSeconds);

                if (eventPipeDataPresent)
                {
                    dumper.ConvertHeapDataToGraph();
                }
            }
            catch (Exception e)
            {
                log.WriteLine($"{getElapsed().TotalSeconds,5:n1}s: [Error] Exception processing events: {e}");
            }

            log.WriteLine("[{0,5:n1}s: Done Dumping .NET heap success={1}]", getElapsed().TotalSeconds, dumpComplete);

            return dumpComplete;
        }

        /// <summary>
        /// PATCHED: Live process collection not supported in CLRScopeMCP (requires internal IpcEndpointConfig)
        /// Use DumpFromEventPipeFile for .nettrace file analysis instead.
        /// </summary>
        public static bool DumpFromEventPipe(CancellationToken ct, int processId, string diagnosticPort, MemoryGraph memoryGraph, TextWriter log, int timeout, DotNetHeapInfo dotNetInfo)
        {
            throw new PlatformNotSupportedException("Live process collection via EventPipe is not supported in CLRScopeMCP. Use DumpFromEventPipeFile for .nettrace file analysis.");
        }
    }
}
