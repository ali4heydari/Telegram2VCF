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
using OpenFileDialog = Microsoft.Win32.OpenFileDialog;

namespace TelegramToVCFExporter
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private string HtmlFilePath { get; set; }
        private string PathToSave { get; set; }
        private BackgroundWorker worker { get; set; } = new BackgroundWorker();

        public MainWindow()
        {
            InitializeComponent();
            worker.WorkerReportsProgress = true;
            worker.WorkerSupportsCancellation = true;
            worker.ProgressChanged += new ProgressChangedEventHandler(worker_ProgressChanged);
            worker.DoWork += new DoWorkEventHandler(worker_DoWork);
            worker.RunWorkerCompleted += worker_RunWorkerCompleted;
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
                //TODO if file dose not exit tell user
                if (File.Exists(fileDialog.FileName))
                {
                    this.HtmlFilePath = fileDialog.FileName;
                    Dispatcher.Invoke(new Action(() => { tbxHtml.Text = fileDialog.FileName; }));
                }
            }).Start();
        }

        private void tbxBrowseSavePath_Click(object sender, RoutedEventArgs e)
        {
            //TODO if file with same name exist tell uesr overwrite or not?
            var thread = new Thread(((() =>
            {
                FolderBrowserDialog savePathFolderBrowserDialog = new FolderBrowserDialog();
                savePathFolderBrowserDialog.SelectedPath =
                    $@"{Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)}";
                savePathFolderBrowserDialog.ShowDialog();
                if (savePathFolderBrowserDialog.SelectedPath != String.Empty)
                {
                    this.PathToSave = savePathFolderBrowserDialog.SelectedPath +
                                      $@"\Telegram_Exported_Contacts_
                                              {DateTime.Now.Year}_
                                              {DateTime.Now.Month}_
                                              {DateTime.Now.Day}.vcf";

                    Dispatcher.Invoke(
                        new Action(() => { tbxSavePath.Text = this.PathToSave; }));
                }
            })));
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
        }

        private void btnExport_Click(object sender, RoutedEventArgs e)
        {
            if (!worker.IsBusy)
            {
                worker.RunWorkerAsync();
            }
            else
            {
                //TODO tell user an other processes is running
                throw new Exception("Background worker is busy");
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
            //TODO ask user really want cancel process
            worker.CancelAsync();
        }

        private void btnAbout_Click(object sender, RoutedEventArgs e)
        {
            //TODO implement about and contact us
        }
    }
}