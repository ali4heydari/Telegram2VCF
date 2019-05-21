using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Media;
using HtmlAgilityPack;
using MessageBox = System.Windows.Forms.MessageBox;
using OpenFileDialog = Microsoft.Win32.OpenFileDialog;

namespace TelegramToVCFExporter
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private string HtmlFilePath { get; set; }

        private string PathToSave { get; set; } =
            Environment.GetFolderPath(Environment.SpecialFolder.Desktop) +
            $@"\Telegram_Exported_Contacts_{DateTime.Now.Year}_{DateTime.Now.Month}_{DateTime.Now.Day}.vcf";

        private BackgroundWorker worker { get; set; } = new BackgroundWorker();

        public MainWindow()
        {
            InitializeComponent();
            worker.WorkerReportsProgress = true;
            worker.WorkerSupportsCancellation = true;
            worker.ProgressChanged += new ProgressChangedEventHandler(worker_ProgressChanged);
            worker.DoWork += new DoWorkEventHandler(worker_DoWork);
            worker.RunWorkerCompleted += worker_RunWorkerCompleted;
            tbxSavePath.Text = PathToSave;
        }

        private void tbxBrowseHtml_Click(object sender, RoutedEventArgs e)
        {
            new Thread(() =>
            {
                OpenFileDialog fileDialog = new OpenFileDialog();
                fileDialog.FileName = "contacts.html";
                fileDialog.Filter = "contacts.html|contacts.html";
                fileDialog.DefaultExt = "html";
                fileDialog.InitialDirectory = $@"{Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)}";
                fileDialog.ShowDialog();
                if (File.Exists(fileDialog.FileName))
                {
                    this.HtmlFilePath = fileDialog.FileName;
                    Dispatcher.Invoke(new Action(() => { tbxHtml.Text = fileDialog.FileName; }));
                }
                else
                {
                    ShowHtmlNotFoundError();
                }
            }).Start();
        }

        private void ShowHtmlNotFoundError()
        {
            MessageBox.Show("File that you selected may move or removed", "File not found",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }

        private void tbxBrowseSavePath_Click(object sender, RoutedEventArgs e)
        {
            var thread = new Thread(((() =>
            {
                FolderBrowserDialog savePathFolderBrowserDialog = new FolderBrowserDialog();
                savePathFolderBrowserDialog.SelectedPath =
                    $@"{Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)}";
                savePathFolderBrowserDialog.ShowDialog();
                if (savePathFolderBrowserDialog.SelectedPath != String.Empty)
                {
                    string savePath = savePathFolderBrowserDialog.SelectedPath +
                                      $@"\Telegram_Exported_Contacts_{DateTime.Now.Year}_{DateTime.Now.Month}_{
                                              DateTime.Now.Day
                                          }.vcf";
                    this.PathToSave = savePath;

                    Dispatcher.Invoke(
                        new Action(() => { tbxSavePath.Text = this.PathToSave; }));
                }
            })));
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
        }

        private void btnExport_Click(object sender, RoutedEventArgs e)
        {
            if (File.Exists(this.PathToSave))
            {
                DialogResult dialogResult = MessageBox.Show(
                    "An other file with same name exist on this directory would you like to overwrite it?",
                    "Overwrite warning", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);
                if (dialogResult == System.Windows.Forms.DialogResult.No)
                {
                    this.PathToSave += "_(1)";
                    Dispatcher.Invoke(new Action(() => { tbxBrowseSavePath.Content = this.PathToSave; }));
                }
                else if (dialogResult == System.Windows.Forms.DialogResult.Cancel)
                {
                    return;
                }
            }

            if (!worker.IsBusy)
            {
                worker.RunWorkerAsync();
            }
            else
            {
                DialogResult dialogResult = MessageBox.Show("An other process is running would you like to cancel it?",
                    "process alter",
                    MessageBoxButtons.YesNo, MessageBoxIcon.Warning);

                if (dialogResult == System.Windows.Forms.DialogResult.Yes)
                {
                    worker.CancelAsync();
                    btnExport_Click(sender, e);
                }
            }
        }

        void worker_ProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            Dispatcher.Invoke(new Action((() =>
            {
                progressbar.Value = e.ProgressPercentage;
                lblProgressStatus.Content = $"{e.ProgressPercentage}%";
            })));
        }

        private void worker_DoWork(object sender, DoWorkEventArgs e)
        {
            if (HtmlFilePath == null)
            {
                MessageBox.Show("File that you selected may move or removed", "File not found",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                worker.CancelAsync();
            }
            else
            {
                Dispatcher.Invoke(new Action((() => { progressbar.Foreground = Brushes.Green; })));

                HtmlAgilityPack.HtmlDocument document = new HtmlAgilityPack.HtmlDocument();

                string readAllText = File.ReadAllText(HtmlFilePath);

                document.LoadHtml(readAllText);

                IList<HtmlNode> bodyNodes =
                    document.QuerySelectorAll("body > div > div.page_body.list_page > div.entry_list > div > div.body");

                List<Tuple<string, string>> contactTuples =
                    bodyNodes
                        .Select(x =>
                            new Tuple<string, string>(
                                x.ChildNodes[3]
                                    .InnerText
                                    .Trim(),
                                x.ChildNodes[5]
                                    .InnerText
                                    .Trim()
                                    .Replace(" ", "")))
                        .ToList();


                List<Contact> contacts = new List<Contact>();

                for (int i = 0; i < contactTuples.Count(); i++)
                    contacts
                        .Add(new Contact(
                            contactTuples[i]
                                .Item1,
                            contactTuples[i]
                                .Item2));

                if (worker.CancellationPending == true)
                {
                    e.Cancel = true;
                    return;
                }

                Dispatcher.Invoke(new Action((() =>
                {
                    dtaGrdContacts.ItemsSource = contacts.Select(c => new
                    {
                        FirstAndLastName = c.FirstAndLastName,
                        PhoneNumber = c.PhoneNumber,
                    }).ToList();
                })));

                using (StreamWriter writer = new StreamWriter(this.PathToSave))
                {
                    for (int i = 0; i < contacts.Count; i++)
                    {
                        if (worker.CancellationPending == true)
                        {
                            e.Cancel = true;
                            return;
                        }

                        writer.Write(contacts[i].VCF);
                        writer.Write("\n");
                        worker.ReportProgress((int) (((i + 1) * 1.0 / contacts.Count) * 100));
                    }
                }
            }
        }

        private void worker_RunWorkerCompleted(object sender,
            RunWorkerCompletedEventArgs e)
        {
            if (e.Error != null)
            {
                progressbar.Foreground = Brushes.Red;
                lblProgressStatus.Content = $"Error: {e.Error.Message}";
            }
            else if (e.Cancelled)
            {
                progressbar.Foreground = Brushes.Yellow;
                lblProgressStatus.Content = $"Export canceled by user";
            }
            else
            {
                lblProgressStatus.Content = $"Export successful";
            }
        }

        private void btnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult dialogResult = MessageBox.Show("Are you really want to cancel the process?", "Cancel confirm",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2);
            if (dialogResult == System.Windows.Forms.DialogResult.Yes)
                worker.CancelAsync();
        }

        private void btnAbout_Click(object sender, RoutedEventArgs e)
        {
            //TODO implement About and Contact us
        }
    }
}