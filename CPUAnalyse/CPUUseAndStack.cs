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

        public  void Run()
        {
            if(PID == null)
            {
                throw new Exception("pid can not empty");
            }

            try
            {
                int pid = PID.Value;

                var sb = new StringBuilder();
                Process process = Process.GetProcessById(pid);
                var counters = new Dictionary<string, ThreadCounterInfo>();
                var threadInfos = new Dictionary<string, ThreadOutputInfo>();

                sb.AppendFormat(
                    @"<html><head><title>{0}</title><style type=""text/css"">table, td{{border: 1px solid #000;border-collapse: collapse;}}</style></head><body>",
                    process.ProcessName);

                #region 计数器

                Console.WriteLine("开始收集计数器...");

                var cate = new PerformanceCounterCategory("Thread");
                string[] instances = cate.GetInstanceNames();
                foreach (string instance in instances)
                {
                    if (instance.StartsWith(process.ProcessName, StringComparison.CurrentCultureIgnoreCase))
                    {
                        var counter1 =
                            new PerformanceCounter("Thread", "ID Thread", instance, true);
                        var counter2 =
                            new PerformanceCounter("Thread", "% Processor Time", instance, true);
                        counters.Add(instance, new ThreadCounterInfo(counter1, counter2));
                        
                    }
                }

                foreach (var pair in counters)
                {
                    try
                    {
                        var info = new ThreadOutputInfo();

                        info.Id = pair.Value.IdCounter.NextValue().ToString();
                        info.ProcessorTimePercentage = pair.Value.ProcessorTimeCounter.NextValue().ToString("0.0");

                        threadInfos.Add(info.Id, info);
                        Console.WriteLine($"已初始化{info.Id}样本");
                    }
                    catch (Exception ex)
                    {
                        WriteErrorLog("性能计数器初始化样本时出错:" + ex.ToString() + Environment.NewLine);
                        Console.WriteLine($"性能计数器初始化样本时有出错,具体请看Log {LogFile} 文件");
                    }
                }

                #endregion

                // 连续抓起样本
                PointInfo(pid, counters, threadInfos, Count, Interval);

                #region 线程信息

                Console.WriteLine("开始收集线程信息...");
                ProcessThreadCollection collection = process.Threads;
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

                        Console.WriteLine($"已收集线程 {thread.Id} 信息");
                    }
                    catch(Exception ex)
                    {
                        WriteErrorLog("获取线程的CPU时间时出错:" + ex.ToString() + Environment.NewLine);
                        Console.WriteLine($"获取线程的CPU时间时有出错,具体请看Log {LogFile} 文件");
                    }
                }

                #endregion

                #region 报表

                try
                {
                    Console.WriteLine("开始生成报表...");

                    var infoList = threadInfos.Values.ToList()
                        .Where(p => !string.IsNullOrWhiteSpace(p.ProcessorTimePercentage) && float.Parse(p.ProcessorTimePercentage) > FilterCPUUse )
                        .ToList();
                    infoList.Sort(new ThreadOutputCompare());
                    foreach (var info in infoList)
                    {
                        sb.Append(info.ToString());
                        sb.Append("<hr />");
                    }
                    sb.Append("</body></html>");
                    
                    using (var sw = new StreamWriter(process.ProcessName + ".htm", false,
                                                     Encoding.Default))
                    {
                        sw.Write(sb.ToString());
                    }

                    Console.WriteLine("已生成报表");

                    Process.Start(process.ProcessName + ".htm");
                }
                catch(Exception ex)
                {
                    WriteErrorLog("生成报表时出错:" + ex.ToString() + Environment.NewLine);
                    Console.WriteLine($"生成报表时有出错,具体请看Log {LogFile} 文件");
                }

                #endregion
            }
            catch (Exception ex)
            {
                WriteErrorLog("其它出错:" + ex.ToString() + Environment.NewLine);
                Console.WriteLine($"其它出错,具体请看Log {LogFile} 文件");
            }
        }

        private  void PointInfo(int pid, Dictionary<string, ThreadCounterInfo> counters, Dictionary<string, ThreadOutputInfo> threadInfos, int count, int interval)
        {
            var index = 0;
            while (count > 0)
            {
                count--;
                index++;
                Thread.Sleep(interval);
                foreach (var pair in counters)
                {
                    try
                    {
                        var threadID = pair.Value.IdCounter.NextValue().ToString();
                        var processorTimePercentage = pair.Value.ProcessorTimeCounter.NextValue().ToString("0.0");

                        Console.WriteLine($"已抓起 {threadID} 的第 {index} 次样本");

                        ThreadOutputInfo info;
                        if (threadInfos.TryGetValue(threadID, out info))
                        {
                            info.ProcessorTimePercentage = processorTimePercentage;
                        }
                    }
                    catch(Exception ex)
                    {
                        WriteErrorLog("性能计数器取样本时出错:" + ex.ToString() + Environment.NewLine);
                        Console.WriteLine($"性能计数器取样本时有出错,具体请看Log {LogFile} 文件");
                    }
                }


                var debugger = new MDbgEngine();
                MDbgProcess proc = null;
                try
                {
                    Console.WriteLine($"开始附加到进程 {pid} 以获取堆栈");

                    proc = debugger.Attach(pid);
                    DrainAttach(debugger, proc);

                    Console.WriteLine($"已附加到进程 {pid}");

                    MDbgThreadCollection tc = proc.Threads;
                    foreach (MDbgThread t in tc)
                    {
                        try
                        {
                            Console.WriteLine($"开始获取线程 {t.Id} 的当前堆栈");

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

                            Console.WriteLine($"已获取线程 {t.Number} 的当前堆栈");
                        }
                        catch (Exception ee)
                        {
                            WriteErrorLog("获取堆栈时出错:" + ee.ToString() + Environment.NewLine);
                            Console.WriteLine($"获取线程 {t.Number} 的当前堆栈时有出错,具体请看Log {LogFile} 文件");
                        }
                    }
                }
                catch (Exception ex)
                {
                    WriteErrorLog("附加到进程时出错:" + ex.ToString() + Environment.NewLine);
                    Console.WriteLine($"附加到进程时出错,具体请看Log {LogFile} 文件");
                }
                finally
                {
                    if (proc != null)
                    {
                        proc.Detach().WaitOne();
                    }
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
