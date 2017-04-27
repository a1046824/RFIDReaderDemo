using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace RFIDReader
{
    public partial class Form1 : Form
    {
        private DateTime garbage_collected;
        public Form1()
        {
            InitializeComponent();
            this.Run();
        }

        private void Run()
        {
            this.garbage_collected = DateTime.Now;

            while (true)
            {
                Device reader = new Device();
                if(String.IsNullOrWhiteSpace(reader.ID) == false)
                {
                    MessageBox.Show(reader.ID);
                }

                // Run the garbage collector every 60 seconds
                TimeSpan time_since_collection = DateTime.Now - garbage_collected;
                if(time_since_collection.TotalSeconds > 59)
                {
                    GC.Collect();
                    this.garbage_collected = DateTime.Now;
                }
            }
        }
        
    }
}