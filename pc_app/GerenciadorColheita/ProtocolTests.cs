using System;
using System.IO;
using System.Text;

namespace GerenciadorColheita
{
    internal static class ProtocolTests
    {
        private static int Main()
        {
            try
            {
                byte[] image = CreateSampleImage();
                EepromReport report = EepromReport.Parse(image, FirmwareProfile.Version160);

                Assert(report.ChecksumValid, "Checksum deveria ser valido.");
                Assert(report.Plots.Count == 12, "Quantidade de glebas incorreta.");
                Assert(report.Plots[0].Valid, "Gleba 1 deveria ser valida.");
                Assert(report.Plots[0].Variety == "Catuai", "Variedade incorreta.");
                Assert(report.Plots[0].GreenForce == 600, "Forca verde incorreta.");
                Assert(report.Plots[0].CherryForce == 650, "Forca cereja incorreta.");
                Assert(
                    report.Plots[0].DiagnosisDescription == "Aguardar 7 dias",
                    "Descricao do diagnostico incorreta.");
                Assert(report.Plots[0].BrakeKilograms == 8, "Freio incorreto.");
                Assert(report.Plots[0].Vibration == 52, "Vibracao incorreta.");
                Assert(report.Plots[0].SpeedMetersPerHour == 1200, "Velocidade incorreta.");

                byte[] legacyImage = (byte[])image.Clone();
                legacyImage[10] = 3;
                legacyImage[18] = 8;
                legacyImage[19] = 75;
                legacyImage[20] = 160;
                legacyImage[252] = 0xFF;
                EepromReport legacy = EepromReport.Parse(
                    legacyImage, FirmwareProfile.Version153);
                Assert(legacy.Plots[0].Harvester == "Korvan", "Colhedora incorreta.");
                Assert(legacy.Plots[0].Vibration == 750, "Vibracao legada incorreta.");
                Assert(legacy.Plots[0].SpeedMetersPerHour == 1600,
                    "Velocidade legada incorreta.");
                Assert(legacy.Plots[0].DiagnosisDescription ==
                    "Colheita seletiva imediata", "Diagnostico legado incorreto.");
                Assert(!legacy.ChecksumAvailable, "Checksum legado deveria estar indisponivel.");

                string outputDirectory = AppDomain.CurrentDomain.BaseDirectory;
                string pdfPath = Path.Combine(outputDirectory, "relatorio_teste.pdf");
                string csvPath = Path.Combine(outputDirectory, "relatorio_teste.csv");
                string legacyPdfPath = Path.Combine(outputDirectory, "relatorio_legado_teste.pdf");
                PdfReportWriter.Write(pdfPath, report);
                PdfReportWriter.Write(legacyPdfPath, legacy);
                report.SaveCsv(csvPath);

                byte[] pdf = File.ReadAllBytes(pdfPath);
                Assert(pdf.Length > 500, "PDF gerado esta vazio.");
                Assert(
                    Encoding.ASCII.GetString(pdf, 0, 8) == "%PDF-1.4",
                    "Cabecalho PDF invalido.");
                Assert(File.ReadAllText(csvPath).Contains("Catuai"), "CSV sem dados da gleba.");
                Assert(File.ReadAllBytes(legacyPdfPath).Length > 500,
                    "PDF legado gerado esta vazio.");

                Console.WriteLine("ProtocolTests: OK");
                return 0;
            }
            catch (Exception error)
            {
                Console.Error.WriteLine("ProtocolTests: FALHA");
                Console.Error.WriteLine(error);
                return 1;
            }
        }

        private static byte[] CreateSampleImage()
        {
            byte[] image = new byte[EepromReport.ImageSize];
            for (int index = 0; index < image.Length; index++)
                image[index] = 0xFF;

            image[0] = 28;
            image[1] = 7;
            image[2] = 26;
            image[3] = 1;
            image[4] = 10;
            image[5] = 35;
            image[6] = 30;
            image[7] = 2;
            image[8] = 30;
            image[9] = 45;
            image[10] = 8;
            image[11] = 8;
            image[12] = 5;
            image[13] = 120;
            image[14] = 0x58;
            image[15] = 0x02;
            image[16] = 0x8A;
            image[17] = 0x02;
            image[18] = 4;
            image[19] = 52;
            image[20] = 120;
            image[253] = 28;
            image[254] = 7;
            image[255] = 26;

            byte checksum = 0;
            for (int index = 0; index < EepromReport.ChecksumAddress; index++)
                checksum ^= image[index];
            image[EepromReport.ChecksumAddress] = checksum;

            return image;
        }

        private static void Assert(bool condition, string message)
        {
            if (!condition)
                throw new InvalidOperationException(message);
        }
    }
}
