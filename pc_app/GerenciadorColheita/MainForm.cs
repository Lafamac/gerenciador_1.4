using System;
using System.ComponentModel;
using System.Drawing;
using System.IO;
using System.Threading;
using System.Windows.Forms;

namespace GerenciadorColheita
{
    internal sealed class MainForm : Form
    {
        private const ushort VendorId = 0x04D8;
        private const ushort ProductId = 0x0011;
        private const byte DownloadLegacyCommand = 1;
        private const byte DownloadWithChecksumCommand = 2;
        private const byte DisconnectCommand = 0;

        private readonly ComboBox firmwareVersion;
        private readonly Button downloadButton;
        private readonly Button rawButton;
        private readonly Button csvButton;
        private readonly Button pdfButton;
        private readonly Label statusLabel;
        private readonly ProgressBar progress;
        private readonly DataGridView grid;
        private readonly BackgroundWorker downloader;
        private EepromReport currentReport;

        public MainForm()
        {
            Text = "Gerenciador de Colheita";
            StartPosition = FormStartPosition.CenterScreen;
            MinimumSize = new Size(980, 580);
            Size = new Size(1120, 680);

            FlowLayoutPanel commands = new FlowLayoutPanel();
            commands.Dock = DockStyle.Top;
            commands.Height = 48;
            commands.Padding = new Padding(8);

            Label versionLabel = new Label();
            versionLabel.Text = "Versao do equipamento:";
            versionLabel.AutoSize = true;
            versionLabel.Margin = new Padding(3, 7, 3, 0);
            firmwareVersion = new ComboBox();
            firmwareVersion.DropDownStyle = ComboBoxStyle.DropDownList;
            firmwareVersion.Width = 90;
            firmwareVersion.Items.Add("1.5.3");
            firmwareVersion.Items.Add("1.6");
            firmwareVersion.SelectedIndex = 1;

            downloadButton = CreateButton("Baixar dados", DownloadClicked);
            rawButton = CreateButton("Salvar EEPROM", SaveRawClicked);
            csvButton = CreateButton("Exportar CSV", SaveCsvClicked);
            pdfButton = CreateButton("Gerar PDF", SavePdfClicked);
            commands.Controls.Add(versionLabel);
            commands.Controls.Add(firmwareVersion);
            commands.Controls.Add(downloadButton);
            commands.Controls.Add(rawButton);
            commands.Controls.Add(csvButton);
            commands.Controls.Add(pdfButton);

            Panel statusPanel = new Panel();
            statusPanel.Dock = DockStyle.Top;
            statusPanel.Height = 48;
            statusLabel = new Label();
            statusLabel.Text = "Conecte o equipamento e clique em Baixar dados.";
            statusLabel.AutoSize = true;
            statusLabel.Location = new Point(12, 6);
            progress = new ProgressBar();
            progress.Location = new Point(12, 25);
            progress.Width = 500;
            progress.Height = 16;
            progress.Minimum = 0;
            progress.Maximum = 32;
            statusPanel.Controls.Add(statusLabel);
            statusPanel.Controls.Add(progress);

            grid = new DataGridView();
            grid.Dock = DockStyle.Fill;
            grid.ReadOnly = true;
            grid.AllowUserToAddRows = false;
            grid.AllowUserToDeleteRows = false;
            grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.DisplayedCells;
            grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            AddColumns();

            Controls.Add(grid);
            Controls.Add(statusPanel);
            Controls.Add(commands);

            downloader = new BackgroundWorker();
            downloader.WorkerReportsProgress = true;
            downloader.DoWork += DownloadData;
            downloader.ProgressChanged += DownloadProgress;
            downloader.RunWorkerCompleted += DownloadCompleted;

            SetExportEnabled(false);
        }

        private static Button CreateButton(string text, EventHandler handler)
        {
            Button button = new Button();
            button.Text = text;
            button.AutoSize = true;
            button.Height = 28;
            button.Click += handler;
            return button;
        }

        private void AddColumns()
        {
            grid.Columns.Add("Number", "Gleba");
            grid.Columns.Add("Valid", "Cadastrada");
            grid.Columns.Add("Date", "Data");
            grid.Columns.Add("Variety", "Variedade");
            grid.Columns.Add("PlantSpacing", "Pes (m)");
            grid.Columns.Add("RowSpacing", "Ruas (m)");
            grid.Columns.Add("Diagnosis", "Diagnostico");
            grid.Columns.Add("GreenForce", "Forca verde");
            grid.Columns.Add("CherryForce", "Forca cereja");
            grid.Columns.Add("Harvester", "Colhedora");
            grid.Columns.Add("Brake", "Freio (kg)");
            grid.Columns.Add("Vibration", "Vibracao");
            grid.Columns.Add("Speed", "Velocidade (m/h)");
        }

        private void DownloadClicked(object sender, EventArgs e)
        {
            if (downloader.IsBusy)
                return;

            currentReport = null;
            grid.Rows.Clear();
            progress.Value = 0;
            downloadButton.Enabled = false;
            SetExportEnabled(false);
            statusLabel.Text = "Procurando equipamento HID...";
            FirmwareProfile profile = firmwareVersion.SelectedIndex == 0
                ? FirmwareProfile.Version153
                : FirmwareProfile.Version160;
            firmwareVersion.Enabled = false;
            downloader.RunWorkerAsync(profile);
        }

        private void DownloadData(object sender, DoWorkEventArgs e)
        {
            FirmwareProfile profile = (FirmwareProfile)e.Argument;
            byte command = profile == FirmwareProfile.Version153
                ? DownloadLegacyCommand
                : DownloadWithChecksumCommand;
            byte[] image = new byte[EepromReport.ImageSize];

            using (HidDevice device = HidDevice.Open(VendorId, ProductId))
            {
                Thread.Sleep(150);
                device.WriteCommand(command);

                for (int packet = 0; packet < 32; packet++)
                {
                    int timeout = packet == 0 ? 6000 : 3000;
                    byte[] payload;
                    try
                    {
                        payload = device.ReadPayload(timeout);
                    }
                    catch (Exception error)
                    {
                        throw new IOException(
                            string.Format(
                                "Falha ao receber o pacote {0} de 32. " +
                                "No equipamento, abra Descarregar Dados e aguarde USB conectado. {1}",
                                packet + 1,
                                error.Message),
                            error);
                    }

                    Buffer.BlockCopy(payload, 0, image, packet * 8, 8);
                    downloader.ReportProgress(packet + 1);
                }

                try
                {
                    device.WriteCommand(DisconnectCommand);
                }
                catch
                {
                    // The data is complete; disconnect failure must not discard it.
                }
            }

            e.Result = EepromReport.Parse(image, profile);
        }

        private void DownloadProgress(object sender, ProgressChangedEventArgs e)
        {
            progress.Value = e.ProgressPercentage;
            statusLabel.Text = string.Format(
                "Recebendo dados: {0} de 32 pacotes...", e.ProgressPercentage);
        }

        private void DownloadCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            downloadButton.Enabled = true;
            firmwareVersion.Enabled = true;

            if (e.Error != null)
            {
                statusLabel.Text = "Falha: " + e.Error.Message;
                MessageBox.Show(
                    this,
                    e.Error.Message,
                    "Falha no download",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
                return;
            }

            currentReport = (EepromReport)e.Result;
            FillGrid();
            SetExportEnabled(true);

            statusLabel.Text = string.Format(
                "Download concluido. Checksum {0}. Ultima data: {1}.",
                !currentReport.ChecksumAvailable
                    ? "nao disponivel (modo 1.5.3)"
                    : currentReport.ChecksumValid ? "valido" : "invalido",
                currentReport.LastDate);
        }

        private void FillGrid()
        {
            grid.Rows.Clear();
            foreach (PlotRecord plot in currentReport.Plots)
            {
                grid.Rows.Add(
                    plot.Number,
                    plot.Valid ? "Sim" : "Nao",
                    plot.Date,
                    plot.Variety,
                    plot.PlantSpacingMeters.ToString("0.0"),
                    plot.RowSpacingMeters.ToString("0.0"),
                    plot.DiagnosisDescription,
                    plot.GreenForce,
                    plot.CherryForce,
                    plot.Harvester,
                    plot.BrakeKilograms,
                    plot.Vibration,
                    plot.SpeedMetersPerHour);
            }
        }

        private void SaveRawClicked(object sender, EventArgs e)
        {
            if (currentReport == null)
                return;

            SaveFileDialog dialog = new SaveFileDialog();
            dialog.Filter = "Imagem EEPROM (*.bin)|*.bin";
            dialog.FileName = "gerenciador_eeprom.bin";
            if (dialog.ShowDialog(this) == DialogResult.OK)
            {
                File.WriteAllBytes(dialog.FileName, currentReport.RawData);
                statusLabel.Text = "EEPROM salva em " + dialog.FileName;
            }
        }

        private void SaveCsvClicked(object sender, EventArgs e)
        {
            if (currentReport == null)
                return;

            SaveFileDialog dialog = new SaveFileDialog();
            dialog.Filter = "Planilha CSV (*.csv)|*.csv";
            dialog.FileName = "relatorio_colheita.csv";
            if (dialog.ShowDialog(this) == DialogResult.OK)
            {
                currentReport.SaveCsv(dialog.FileName);
                statusLabel.Text = "CSV salvo em " + dialog.FileName;
            }
        }

        private void SavePdfClicked(object sender, EventArgs e)
        {
            if (currentReport == null)
                return;

            SaveFileDialog dialog = new SaveFileDialog();
            dialog.Filter = "Documento PDF (*.pdf)|*.pdf";
            dialog.FileName = "relatorio_colheita.pdf";
            if (dialog.ShowDialog(this) == DialogResult.OK)
            {
                PdfReportWriter.Write(dialog.FileName, currentReport);
                statusLabel.Text = "PDF salvo em " + dialog.FileName;
            }
        }

        private void SetExportEnabled(bool enabled)
        {
            rawButton.Enabled = enabled;
            csvButton.Enabled = enabled;
            pdfButton.Enabled = enabled;
        }
    }
}
