using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace CPUAnalyse
{
    public partial class MainFrm : Form
    {
        public MainFrm()
        {
            InitializeComponent();
            CheckForIllegalCrossThreadCalls = false;
        }

        private void MainFrm_Load(object sender, EventArgs e)
        {

        }

        private void btnGo_Click(object sender, EventArgs e)
        {
            if(!InputCheck())
            {
                return;
            }

            int count = int.Parse(txtCount.Text);
            int interval = int.Parse(txtInterval.Text);
            int pid = int.Parse(txtPID.Text);
            float cpuUseFilter = float.Parse(txtCpuUseFilter.Text);

            CPUUseAndStack cpuAnalyse = new CPUUseAndStack();
            cpuAnalyse.Count = count;
            cpuAnalyse.Interval = interval;
            cpuAnalyse.PID = pid;
            cpuAnalyse.FilterCPUUse = cpuUseFilter;
            try
            {
                cpuAnalyse.Run();
            }
            catch(Exception ex)
            {
                MessageBox.Show(ex.ToString());
            }
        }

        private bool InputCheck()
        {
            int value;
            if(string.IsNullOrWhiteSpace(txtCount.Text) || !int.TryParse(txtCount.Text, out value))
            {
                MessageBox.Show("取样本次数必须是大于0的整数" );
                return false;
            }

            if (string.IsNullOrWhiteSpace(txtInterval.Text) || !int.TryParse(txtInterval.Text, out value))
            {
                MessageBox.Show("取样间隔必须是大于0的整数");
                return false;
            }

            if (string.IsNullOrWhiteSpace(txtPID.Text) || !int.TryParse(txtPID.Text, out value))
            {
                MessageBox.Show("进程ID必须是大于0的整数");
                return false;
            }

            return true;
        }
    }
}
