using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace GerenciadorColheita
{
    internal enum FirmwareProfile
    {
        Version153,
        Version160
    }

    internal sealed class EepromReport
    {
        public const int ImageSize = 256;
        public const int PlotSize = 21;
        public const int PlotCount = 12;
        public const int ChecksumAddress = 252;

        private static readonly string[] VarietyNames =
        {
            "Mundo Novo", "Catuai", "Catucai", "Acaia", "Icatu",
            "Topazio", "Rubi", "Obata", "Outros"
        };

        private static readonly string[] HarvesterNames =
        {
            "KTR", "Matao", "Matao Tracionada", "Korvan", "CASE",
            "CASE Tracionada", "K3", "TDI", "TDI Tracionada", "Vetor"
        };

        public FirmwareProfile Profile { get; private set; }
        public byte[] RawData { get; private set; }
        public IList<PlotRecord> Plots { get; private set; }
        public byte StoredChecksum { get; private set; }
        public byte CalculatedChecksum { get; private set; }
        public bool ChecksumValid { get { return StoredChecksum == CalculatedChecksum; } }
        public bool ChecksumAvailable { get { return StoredChecksum != 0xFF; } }
        public string LastDate { get; private set; }

        public static EepromReport Parse(byte[] data)
        {
            return Parse(data, FirmwareProfile.Version160);
        }

        public static EepromReport Parse(byte[] data, FirmwareProfile profile)
        {
            if (data == null || data.Length != ImageSize)
                throw new ArgumentException("A imagem da EEPROM deve conter exatamente 256 bytes.");

            EepromReport report = new EepromReport();
            report.Profile = profile;
            report.RawData = (byte[])data.Clone();
            report.StoredChecksum = data[ChecksumAddress];

            byte checksum = 0;
            for (int address = 0; address < ChecksumAddress; address++)
                checksum ^= data[address];
            report.CalculatedChecksum = checksum;

            report.LastDate = FormatDate(data[253], data[254], data[255]);
            List<PlotRecord> plots = new List<PlotRecord>();

            for (int plot = 0; plot < PlotCount; plot++)
            {
                int offset = plot * PlotSize;
                byte diagnosis = data[offset + 18];
                byte variety = data[offset + 3];

                PlotRecord record = new PlotRecord();
                record.Number = plot + 1;
                record.Valid = profile == FirmwareProfile.Version153
                    ? diagnosis >= 1 && diagnosis <= 8
                    : diagnosis >= 1 && diagnosis <= 6;
                record.Date = FormatDate(data[offset], data[offset + 1], data[offset + 2]);
                record.Variety = variety < VarietyNames.Length
                    ? VarietyNames[variety]
                    : "Desconhecida";
                record.PlantSpacingMeters = data[offset + 4] / 10.0;
                record.RowSpacingMeters = data[offset + 5] / 10.0;
                record.PlantHeightMeters = data[offset + 6] / 10.0;
                record.BranchesPerPlant = data[offset + 7];
                record.CherryPercent = data[offset + 8];
                record.GreenPercent = data[offset + 9];
                if (profile == FirmwareProfile.Version153)
                {
                    byte harvester = data[offset + 10];
                    record.Harvester = harvester < HarvesterNames.Length
                        ? HarvesterNames[harvester]
                        : "Desconhecida";
                    record.BrakeKilograms = 0;
                }
                else
                {
                    record.Harvester = "";
                    record.BrakeKilograms = data[offset + 10];
                }
                record.PendingLoad = data[offset + 11] + (data[offset + 12] / 10.0);
                record.Productivity = data[offset + 13];
                record.GreenForce = data[offset + 14] | (data[offset + 15] << 8);
                record.CherryForce = data[offset + 16] | (data[offset + 17] << 8);
                record.Diagnosis = diagnosis;
                record.DiagnosisDescription = GetDiagnosisDescription(diagnosis, profile);
                record.Vibration = data[offset + 19] * (profile == FirmwareProfile.Version153 ? 10 : 1);
                record.SpeedMetersPerHour = data[offset + 20] * 10;
                plots.Add(record);
            }

            report.Plots = plots;
            return report;
        }

        public void SaveCsv(string path)
        {
            using (StreamWriter writer = new StreamWriter(path, false, Encoding.UTF8))
            {
                writer.WriteLine(
                    "Gleba;Valida;Data;Variedade;EspacamentoPes_m;EspacamentoRuas_m;" +
                    "Altura_m;Ramos;Cereja_pct;Verde_pct;Colhedora;Freio_kg;CargaPendente;" +
                    "Produtividade;ForcaVerde;ForcaCereja;Diagnostico;Descricao;Vibracao;" +
                    "Velocidade_m_h");

                foreach (PlotRecord plot in Plots)
                {
                    writer.WriteLine(string.Join(";", new[]
                    {
                        plot.Number.ToString(CultureInfo.InvariantCulture),
                        plot.Valid ? "Sim" : "Nao",
                        plot.Date,
                        plot.Variety,
                        plot.PlantSpacingMeters.ToString("0.0", CultureInfo.InvariantCulture),
                        plot.RowSpacingMeters.ToString("0.0", CultureInfo.InvariantCulture),
                        plot.PlantHeightMeters.ToString("0.0", CultureInfo.InvariantCulture),
                        plot.BranchesPerPlant.ToString(CultureInfo.InvariantCulture),
                        plot.CherryPercent.ToString(CultureInfo.InvariantCulture),
                        plot.GreenPercent.ToString(CultureInfo.InvariantCulture),
                        plot.Harvester,
                        plot.BrakeKilograms.ToString(CultureInfo.InvariantCulture),
                        plot.PendingLoad.ToString("0.0", CultureInfo.InvariantCulture),
                        plot.Productivity.ToString(CultureInfo.InvariantCulture),
                        plot.GreenForce.ToString(CultureInfo.InvariantCulture),
                        plot.CherryForce.ToString(CultureInfo.InvariantCulture),
                        plot.Diagnosis.ToString(CultureInfo.InvariantCulture),
                        plot.DiagnosisDescription,
                        plot.Vibration.ToString(CultureInfo.InvariantCulture),
                        plot.SpeedMetersPerHour.ToString(CultureInfo.InvariantCulture)
                    }));
                }
            }
        }

        public IList<string> BuildReportLines()
        {
            List<string> lines = new List<string>();
            lines.Add("GERENCIADOR DE COLHEITA - RELATORIO");
            lines.Add("Data da ultima avaliacao: " + LastDate);
            if (ChecksumAvailable)
            {
                lines.Add(string.Format(
                    "Checksum: armazenado 0x{0:X2}, calculado 0x{1:X2} ({2})",
                    StoredChecksum,
                    CalculatedChecksum,
                    ChecksumValid ? "VALIDO" : "INVALIDO"));
            }
            else
            {
                lines.Add("Checksum: nao disponivel no modo legado");
            }
            lines.Add("");

            foreach (PlotRecord plot in Plots)
            {
                lines.Add(string.Format(
                    "GLEBA {0:00} - {1}", plot.Number, plot.Valid ? "CADASTRADA" : "VAZIA"));

                if (plot.Valid)
                {
                    lines.Add("Data: " + plot.Date + " | Variedade: " + plot.Variety);
                    lines.Add(string.Format(
                        CultureInfo.InvariantCulture,
                        "Espacamento: {0:0.0} m x {1:0.0} m | Altura: {2:0.0} m | Ramos: {3}",
                        plot.PlantSpacingMeters,
                        plot.RowSpacingMeters,
                        plot.PlantHeightMeters,
                        plot.BranchesPerPlant));
                    lines.Add(string.Format(
                        "Cereja: {0}% | Verde: {1}% | Diagnostico: {2}",
                        plot.CherryPercent,
                        plot.GreenPercent,
                        plot.DiagnosisDescription));
                    lines.Add(string.Format(
                        "Forca verde: {0} | Forca cereja: {1}",
                        plot.GreenForce,
                        plot.CherryForce));
                    if (Profile == FirmwareProfile.Version153)
                    {
                        lines.Add("Colhedora: " + plot.Harvester);
                        lines.Add(string.Format(
                            "Recomendacao: vibracao {0} RPM | velocidade {1} m/h",
                            plot.Vibration,
                            plot.SpeedMetersPerHour));
                    }
                    else
                    {
                        lines.Add(string.Format(
                            "Recomendacao: vibracao {0} | velocidade {1} m/h | freio {2} kg",
                            plot.Vibration,
                            plot.SpeedMetersPerHour,
                            plot.BrakeKilograms));
                    }
                }

                lines.Add("");
            }

            return lines;
        }

        private static string FormatDate(byte day, byte month, byte year)
        {
            if (day < 1 || day > 31 || month < 1 || month > 12 || year > 99)
                return "--/--/--";

            return string.Format("{0:00}/{1:00}/{2:00}", day, month, year);
        }

        private static string GetDiagnosisDescription(byte diagnosis, FirmwareProfile profile)
        {
            if (profile == FirmwareProfile.Version153)
            {
                switch (diagnosis)
                {
                    case 1: return "Colheita plena em 21 dias";
                    case 2: return "Colheita plena em 14 dias";
                    case 3: return "Colheita plena em 7 dias";
                    case 4: return "Colheita plena imediata";
                    case 5: return "Colheita seletiva em 21 dias";
                    case 6: return "Colheita seletiva em 14 dias";
                    case 7: return "Colheita seletiva em 7 dias";
                    case 8: return "Colheita seletiva imediata";
                    default: return "Medicao invalida";
                }
            }

            switch (diagnosis)
            {
                case 1: return "Aguardar 28 dias";
                case 2: return "Aguardar 21 dias";
                case 3: return "Aguardar 14 dias";
                case 4: return "Aguardar 7 dias";
                case 5: return "Colheita plena";
                case 6: return "Colheita seletiva";
                default: return "Medicao invalida";
            }
        }
    }

    internal sealed class PlotRecord
    {
        public int Number { get; set; }
        public bool Valid { get; set; }
        public string Date { get; set; }
        public string Variety { get; set; }
        public double PlantSpacingMeters { get; set; }
        public double RowSpacingMeters { get; set; }
        public double PlantHeightMeters { get; set; }
        public int BranchesPerPlant { get; set; }
        public int CherryPercent { get; set; }
        public int GreenPercent { get; set; }
        public string Harvester { get; set; }
        public int BrakeKilograms { get; set; }
        public double PendingLoad { get; set; }
        public int Productivity { get; set; }
        public int GreenForce { get; set; }
        public int CherryForce { get; set; }
        public int Diagnosis { get; set; }
        public string DiagnosisDescription { get; set; }
        public int Vibration { get; set; }
        public int SpeedMetersPerHour { get; set; }
    }
}
