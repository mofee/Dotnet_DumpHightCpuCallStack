using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CPUAnalyse
{
    public class ThreadCounterInfo
    {
        public PerformanceCounter IdCounter;
        public PerformanceCounter ProcessorTimeCounter;

        public ThreadCounterInfo(PerformanceCounter counter1, PerformanceCounter counter2)
        {
            IdCounter = counter1;
            ProcessorTimeCounter = counter2;
        }
    }
}
