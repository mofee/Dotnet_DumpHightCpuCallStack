using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CPUAnalyse
{
    public class ThreadOutputInfo
    {
        public List<string> CallStack = new List<string>();  // 当前堆栈
        public string Id;                                    // 线程ID
        public string ProcessorTimePercentage;               // CPU占用时间百分比
        public string StartTime;                             // 线程开始时间
        public string UserProcessorTime;                     // 线程用户代码运行时间
        public string PrivilegedProcessorTime;               // 线程内核时间
        public string TotalProcessorTime;                    // 线程使用CPU的总时间

        public override string ToString()
        {
            return
                string.Format(
@"<table style=""width: 1000px;"">
    <colgroup>
        <col style=""width: 80px"" />
        <col style=""width: 200px"" />
        <col style=""width: 140px"" />
        <col />
    </colgroup>
    <tr>
        <td>ThreadId</td>
        <td>{0}</td>
        <td>% Processor Time</td>
        <td>{1}</td>
    </tr>
    <tr>
        <td>UserProcessorTime</td>
        <td>{2}</td>
        <td>StartTime</td>
        <td>{3}</td>
    </tr>
    <tr>
        <td>PrivilegedProcessorTime</td>
        <td>{4}</td>
        <td>TotalProcessorTime</td>
        <td>{5}</td>
    </tr>
    <tr>
        <td colspan=""4"">{6}</td>
    </tr>
</table>",
                    Id, ProcessorTimePercentage, 
                    UserProcessorTime, StartTime,
                    PrivilegedProcessorTime, TotalProcessorTime,
                    string.Join("<br/><br/>",CallStack));
        }
    }
}
