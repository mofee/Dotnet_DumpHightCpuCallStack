using Microsoft.Samples.Debugging.MdbgEngine;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace CPUAnalyse
{
    public class CPUUseAndStack
    {
        /// <summary>
        /// 样本数据个数
        /// </summary>
        public  int Count = 2;

        /// <summary>
        /// 抓起样本数据间隔，单位毫秒
        /// </summary>
        public  int Interval = 1000;

        /// <summary>
        /// 只显示CPU时间大于 某个 百分比的线程
        /// </summary>
        public float FilterCPUUse = 1;

        /// <summary>
        /// 进程ID
        /// </summary>
        public int? PID = null;

        /// <summary>
        /// 进程实例后缀
        /// </summary>
        private string ProcessPostFix = string.Empty;

        private Process TheProcess = null;

        private void GetProcessPostFix(string processName)
        {
            // 通过计数器类型 Process 来获取实例编号与进程ID的关系
            var processCate = new PerformanceCounterCategory("Process");
            var processInstances = processCate.GetInstanceNames();
            foreach (var p in processInstances)
            {
                if (p.StartsWith(processName, StringComparison.CurrentCultureIgnoreCase))
                {
                    var counter = new PerformanceCounter("Process", "ID Process", p, true);
                    var currentPID = (int)counter.NextSample().RawValue;
                    if (currentPID == PID)
                    {
                        if (p.Contains("#"))
                        {
                            var charIndex = p.IndexOf("#");
                            this.ProcessPostFix = p.Substring(charIndex);
                        }

                        break;
                    }
                }
            }
        }

        private void CreatePerformanceCounter(string processName, PerformanceCounterCategory cate, Dictionary<string, ThreadCounterInfo> counters)
        {
            string[] instances = cate.GetInstanceNames();
            foreach (string instance in instances)
            {
                if (instance.StartsWith(processName, StringComparison.CurrentCultureIgnoreCase)
                    && ( 
                         (string.IsNullOrWhiteSpace(this.ProcessPostFix) && !instance.Contains("#")) ||
                         (!string.IsNullOrWhiteSpace(this.ProcessPostFix) && instance.EndsWith(this.ProcessPostFix))
                       )
                   )
                {
                    var counter1 =
                        new PerformanceCounter("Thread", "ID Thread", instance, true);
                    var counter2 =
                        new PerformanceCounter("Thread", "% Processor Time", instance, true);
                    counters.Add(instance, new ThreadCounterInfo(counter1, counter2));

                }
            }
        }

        public void Run()
        {
            if (PID == null)
            {
                throw new Exception("pid can not empty");
            }

            try
            {
                Console.WriteLine($"starting {DateTime.Now.ToString("mm: ss.fff")}...");

                int pid = PID.Value;

                var sb = new StringBuilder();
                this.TheProcess = Process.GetProcessById(pid);
                var counters = new Dictionary<string, ThreadCounterInfo>();
                var threadInfos = new Dictionary<string, ThreadOutputInfo>();

                sb.AppendFormat(
                    @"<html><head><title>{0}</title><style type=""text/css"">table, td{{border: 1px solid #000;border-collapse: collapse;}}</style></head><body>",
                    this.TheProcess.ProcessName);

                #region 计数器

                Console.WriteLine("start create performance counter ...");

                Console.WriteLine($"start get process postfix {DateTime.Now.ToString("mm:ss.fff")}...");
                this.GetProcessPostFix(this.TheProcess.ProcessName);
                Console.WriteLine($"end get process postfix {DateTime.Now.ToString("mm:ss.fff")}...");

                var cate = new PerformanceCounterCategory("Thread");
                CreatePerformanceCounter(this.TheProcess.ProcessName, cate, counters);

                #endregion

                // 连续抓起样本
                PointInfo(pid, counters, threadInfos, Count, Interval, cate);

                #region 线程信息

                Console.WriteLine("start collect thread info ...");
                ProcessThreadCollection collection = this.TheProcess.Threads;
                foreach (ProcessThread thread in collection)
                {
                    try
                    {
                        ThreadOutputInfo info;
                        if (threadInfos.TryGetValue(thread.Id.ToString(), out info))
                        {
                            info.UserProcessorTime = thread.UserProcessorTime.ToString();
                            info.TotalProcessorTime = thread.TotalProcessorTime.ToString();
                            info.PrivilegedProcessorTime = thread.PrivilegedProcessorTime.ToString();
                            info.StartTime = thread.StartTime.ToString();
                        }

                        Console.WriteLine($"had collect thread {thread.Id} info");
                    }
                    catch (Exception ex)
                    {
                        WriteErrorLog("获取线程的CPU时间时出错:" + ex.ToString() + Environment.NewLine);
                        Console.WriteLine($"an exception occur when get thread info, Log file {LogFile} for detail");
                    }
                }

                #endregion

                #region 报表

                try
                {
                    Console.WriteLine("start generating report ...");

                    var infoList = threadInfos.Values.ToList()
                        .Where(p => !string.IsNullOrWhiteSpace(p.ProcessorTimePercentage) && float.Parse(p.ProcessorTimePercentage) > FilterCPUUse)
                        .ToList();
                    infoList.Sort(new ThreadOutputCompare());
                    foreach (var info in infoList)
                    {
                        sb.Append(info.ToString());
                        sb.Append("<hr />");
                    }
                    sb.Append("</body></html>");

                    using (var sw = new StreamWriter(this.TheProcess.ProcessName + ".htm", false,
                                                     Encoding.Default))
                    {
                        sw.Write(sb.ToString());
                    }

                    Console.WriteLine("had generated report");

                    var path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, $"{this.TheProcess.ProcessName}_{DateTime.Now.ToString("yyyyMMddhhmmss")}.htm");
                    File.WriteAllText(path, sb.ToString());

                    Process.Start($"{this.TheProcess.ProcessName}.htm");
                }
                catch (Exception ex)
                {
                    WriteErrorLog("生成报表时出错:" + ex.ToString() + Environment.NewLine);
                    Console.WriteLine($"an exception occur when generated report, Log file {LogFile} for detail");
                }

                #endregion

                Console.WriteLine($"end all {DateTime.Now.ToString("mm: ss.fff")}");
            }
            catch (Exception ex)
            {
                WriteErrorLog("其它出错:" + ex.ToString() + Environment.NewLine);
                Console.WriteLine($"an exception occur,Log file {LogFile} for detail");
            }
        }


        private List<string> GetCurrentThreadIDList()
        {
            var ths = this.TheProcess.Threads;
            var result = new List<string>();
            foreach(ProcessThread t in ths)
            {
                result.Add(t.Id.ToString());
            }

            return result;
        }

        private  void PointInfo(int pid, Dictionary<string, ThreadCounterInfo> counters,
                                Dictionary<string, ThreadOutputInfo> threadInfos, int count, int interval,
                                PerformanceCounterCategory cate)
        {
            var index = 0;
            while (count > 0)
            {
                count--;
                index++;

                var threadIdList = GetCurrentThreadIDList();
                var willRemoveCounter = new List<string>();
                foreach (var pair in counters)
                {
                    try
                    {
                        // 第一次因为没有 threadID，所以要用 InstanceExists 来判断
                        if (index == 1 && !cate.InstanceExists(pair.Key))
                        {
                            willRemoveCounter.Add(pair.Key);
                            Console.WriteLine($"thread {pair.Value?.ThreadID ?? string.Empty} not exist when get {index} sample");
                            continue;
                        }
                        else if(index > 1)// 第二次及以上使用另外方法判断，因为第一种方式慢
                        {
                            if(!threadIdList.Contains(pair.Value.ThreadID))
                            {
                                willRemoveCounter.Add(pair.Key);
                                Console.WriteLine($"thread {pair.Value.ThreadID} not exist when get {index} sample");
                                continue;
                            }
                        }

                        // cpu使用率
                        var processorTimeCounterValue = pair.Value.ProcessorTimeCounter.NextValue();

                        // 第一次，创建 ThreadOutputInfo 对象
                        if (index == 1)
                        {
                            var info = new ThreadOutputInfo();
                            info.ProcessorTimePercentage = processorTimeCounterValue.ToString("0.0");
                            info.Id = pair.Value.IdCounter.NextValue().ToString();
                            pair.Value.ThreadID = info.Id;
                            threadInfos.Add(info.Id, info);
                        }
                        // 第二次及以上，使用率低的排除掉
                        else if (processorTimeCounterValue < FilterCPUUse)
                        {
                            willRemoveCounter.Add(pair.Key);
                        }
                        else // 第二次及以上，不需要排除的，更新 ThreadOutputInfo 对象 
                        {
                            ThreadOutputInfo info;
                            if (threadInfos.TryGetValue(pair.Value.ThreadID, out info))
                            {
                                info.ProcessorTimePercentage = processorTimeCounterValue.ToString("0.0");
                            }
                        }
                        
                        Console.WriteLine($"had got {pair.Value.ThreadID}'s {index} sample");                        
                    }
                    catch(Exception ex)
                    {
                        willRemoveCounter.Add(pair.Key);
                        WriteErrorLog("Performance counter has an exception when get sample:" + ex.ToString() + Environment.NewLine);
                        Console.WriteLine($"an exception occur when get sample, log file {LogFile} for detail");
                    }
                }

                // 删除已不存在或出错的实例计数器
                willRemoveCounter.ForEach(p => counters.Remove(p));

                // 获取堆栈
                GetCurrentCallStack(counters, threadInfos);

                // 睡眠指定间隔
                Thread.Sleep(interval);
            }
        }

        private void GetCurrentCallStack(Dictionary<string, ThreadCounterInfo> counters, Dictionary<string, ThreadOutputInfo> threadInfos)
        {
            var debugger = new MDbgEngine();
            MDbgProcess proc = null;
            try
            {
                Console.WriteLine($"start attack to {PID} to get call stack");

                proc = debugger.Attach((int)PID);
                DrainAttach(debugger, proc);

                Console.WriteLine($"had attacked to {PID}");

                var counterThreadIDList = counters.Select(p => p.Value.ThreadID).ToList();
                MDbgThreadCollection tc = proc.Threads;
                foreach (MDbgThread t in tc)
                {
                    try
                    {
                        // 计数器没有的线程，不拿堆栈
                        if (!counterThreadIDList.Contains(t.Id.ToString()))
                        {
                            continue;
                        }

                        Console.WriteLine($"start get {t.Id}'s call stack");

                        var tempStrs = new StringBuilder();
                        foreach (MDbgFrame f in t.Frames)
                        {
                            tempStrs.AppendFormat("<br />" + f);
                        }
                        ThreadOutputInfo info;
                        if (threadInfos.TryGetValue(t.Id.ToString(), out info))
                        {
                            var stack = $"{DateTime.Now.ToString()}<br/>";
                            stack = stack + (tempStrs.Length == 0 ? "no managment call stack" : tempStrs.ToString());
                            info.CallStack.Add(stack);
                        }

                        Console.WriteLine($"had got {t.Id}'s call stack");
                    }
                    catch (Exception ee)
                    {
                        WriteErrorLog("获取堆栈时出错:" + ee.ToString() + Environment.NewLine);
                        Console.WriteLine($"an exception occur when got {t.Number}'s call stack, Log file {LogFile} for detail");
                    }
                }
            }
            catch (Exception ex)
            {
                WriteErrorLog("附加到进程时出错:" + ex.ToString() + Environment.NewLine);
                Console.WriteLine($"an exception occur when attack to process, Log file {LogFile} for detail");
            }
            finally
            {
                if (proc != null)
                {
                    proc.Detach().WaitOne();
                }
            }
        }

        private  void DrainAttach(MDbgEngine debugger, MDbgProcess proc)
        {
            bool fOldStatus = debugger.Options.StopOnNewThread;

            debugger.Options.StopOnNewThread = false; // skip while waiting for AttachComplete
            proc.Go().WaitOne();
            Debug.Assert(proc.StopReason is AttachCompleteStopReason);

            debugger.Options.StopOnNewThread = true; // needed for attach= true; // needed for attach

            // Drain the rest of the thread create events.
            while (proc.CorProcess.HasQueuedCallbacks(null))
            {
                proc.Go().WaitOne();
                Debug.Assert(proc.StopReason is ThreadCreatedStopReason);
            }

            debugger.Options.StopOnNewThread = fOldStatus;
        }

        private string LogFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, $"{DateTime.Now.ToString("yyyyMMddHHmmss")}.txt");
        private bool IsHasError = false;

        private void WriteErrorLog(string msg)
        {
            IsHasError = true;
            File.AppendAllText(LogFile, msg);
        }

    }

    public class ThreadOutputCompare : IComparer<ThreadOutputInfo>
    {
        public int Compare(ThreadOutputInfo x, ThreadOutputInfo y)
        {
            
            if (x.ProcessorTimePercentage == y.ProcessorTimePercentage)
            {
                return 0;
            }
            else if (float.Parse(x.ProcessorTimePercentage) > float.Parse(y.ProcessorTimePercentage))
            {
                return -1;
            }
            
            return 1;            
        }
        
    }

}
