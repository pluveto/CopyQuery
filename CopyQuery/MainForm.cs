using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace CopyQuery
{
    public partial class MainForm : Form
    {
        IDictionary<String, String> History;
        IDictionary<String, long> entOffset;
        StreamWriter logwrite;
        KeyboardHook hook;
        KeyboardHook hookCancel;
        string path = "ecdict.csv";
        public MainForm()
        {
            InitializeComponent();

            History = new Dictionary<String, String>();
            entOffset = new Dictionary<String, long>();

            hook = new KeyboardHook();
            hook.RegisterHotKey(KeyboardHook.ModifierKeys.Control, Keys.Q);
            hook.KeyPressed += Query_Hook_KeyPressed;

            hookCancel = new KeyboardHook();
            hookCancel.RegisterHotKey(KeyboardHook.ModifierKeys.Control, Keys.W);
            hookCancel.KeyPressed += HookCancel_KeyPressed;

            preload();
            logwrite = new StreamWriter("history.log", true);
        }
        ~MainForm()
        {
            hook.Dispose();
            hookCancel.Dispose();
            logwrite.Close();
        }
        private void preload()
        {
            using (StreamReader sr = new StreamReader(path))
            {
                sr.DiscardBufferedData();

                while (sr.Peek() >= 0)
                {
                    var offsetInFile = ActualPosition(sr);
                    var line = sr.ReadLine();
                    var parts = splitCsvLine(line);
                    var key = parts[0].Trim().ToLower();
                    var value = offsetInFile;
                    entOffset[key] = value;
                }
            }
        }


        private void HookCancel_KeyPressed(object sender, KeyboardHook.KeyPressedEventArgs e)
        {
            if(History.Count == 0)
            {
                return;
            }
            var last = History.Last();
            History.Remove(last.Key);
            onHistoryChanged();
        }

        readonly static FieldInfo charPosField = typeof(StreamReader).GetField("charPos", BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly);
        readonly static FieldInfo charLenField = typeof(StreamReader).GetField("charLen", BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly);
        readonly static FieldInfo charBufferField = typeof(StreamReader).GetField("charBuffer", BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly);

        static long ActualPosition(StreamReader reader)
        {
            var charBuffer = (char[])charBufferField.GetValue(reader);
            var charLen = (int)charLenField.GetValue(reader);
            var charPos = (int)charPosField.GetValue(reader);

            return reader.BaseStream.Position - reader.CurrentEncoding.GetByteCount(charBuffer, charPos, charLen - charPos);
        }
        public String GetLineAtOffset(string path, long offset)
        {
            using (StreamReader sr = new StreamReader(path))
            {
                sr.BaseStream.Seek(offset, SeekOrigin.Begin);
                return sr.ReadLine();
            }
        }

        private void onHistoryChanged()
        {
            var sb = new StringBuilder();
            var ents = History.ToArray();
            var maxLen = 0;
            foreach(var ent in ents)
            {
                if(ent.Key.Length > maxLen)
                {
                    maxLen = ent.Key.Length;
                }
            }
            listHistory.Invoke((MethodInvoker)delegate {
                textBox1.Clear();
                listHistory.Items.Clear();
            });

            foreach (var ent in ents )
            {
                sb.Append(ent.Key.PadRight(maxLen+1));
                sb.Append(ent.Value);
                sb.AppendLine();

                var item = new ListViewItem();
                item.Text = ent.Key;
                item.SubItems.Add(ent.Value);

                listHistory.Invoke((MethodInvoker)delegate {
                    listHistory.Items.Insert(0, item);
                });
            }
            textBox1.Invoke((MethodInvoker)delegate {
                textBox1.Text = sb.ToString().Replace("\\n","\n");
            });
        }

        private string[] splitCsvLine(string line)
        {
            var inQuote = false;
            var parts = new List<string>();
            var part = new StringBuilder();
            for (int i = 0; i < line.Length; i++)
            {
                var c = line[i];
                if (c == '"')
                {
                    inQuote = !inQuote;
                }
                else if (c == ',' && !inQuote)
                {
                    parts.Add(part.ToString());
                    part.Clear();
                }
                else
                {
                    part.Append(c);
                }
            }
            parts.Add(part.ToString());
            return parts.ToArray();
        }

        private void Query_Hook_KeyPressed(object sender, KeyboardHook.KeyPressedEventArgs e)
        {
            Thread thread = new Thread(async () => {
                SendInput.CtrlC();
                Thread.Sleep(50);
                var clipboard = Clipboard.GetText().Trim().ToLower();
                if (clipboard == string.Empty)
                {
                    MessageBox.Show("NOTHING");
                    return;
                }
                
                long offset;
                if (!entOffset.TryGetValue(clipboard, out offset)) {
                    MessageBox.Show("NO ENTRY");
                    return;
                }
                var line = GetLineAtOffset(path, offset);
                var parts = splitCsvLine(line);
                var columns = "word,phonetic,definition,translation,pos,collins,oxford,tag,bnc,frq,exchange,detail,audio";
                var columnsArr = columns.Split(',');
                var dict = new Dictionary<string, string>();
                for (int i = 0; i < Math.Min(columnsArr.Length, parts.Length); i++)
                {
                    dict.Add(columnsArr[i], parts[i]);
                }
                var prefix = "";
                if(dict["phonetic"].Length > 0)
                {
                    prefix += "[" + dict["phonetic"] + "] ";
                }
                var val = prefix + dict["translation"];

                History[clipboard]= val;
                var time = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                logwrite.WriteLine(time + "," + clipboard + ",\"" + val+"\"");
                logwrite.Flush();
                onHistoryChanged();
            });
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
        }

        private void button1_Click(object sender, EventArgs e)
        {
            Clipboard.SetText(textBox1.Text);
        }

        private void Form1_Load(object sender, EventArgs e)
        {

        }

        private void button2_Click(object sender, EventArgs e)
        {
            textBox1.Clear();
            History.Clear();
            listHistory.Items.Clear();
        }

        private void checkBox1_CheckedChanged(object sender, EventArgs e)
        {
            TopMost = checkBox1.Checked;
        }

        private void listView1_KeyPress(object sender, KeyPressEventArgs e)
        {
            if(e.KeyChar == 'd')
            {
                if(listHistory.SelectedItems.Count == 0)
                {
                    return;
                }
                var sel = listHistory.SelectedItems;
                foreach(ListViewItem en in sel)
                {
                    History.Remove(en.Text);
                }
                onHistoryChanged();
            }
        }
    }
}
