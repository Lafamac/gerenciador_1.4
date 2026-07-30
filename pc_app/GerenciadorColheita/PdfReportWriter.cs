using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace GerenciadorColheita
{
    internal static class PdfReportWriter
    {
        private const int LinesPerPage = 48;

        public static void Write(string path, EepromReport report)
        {
            IList<string> lines = report.BuildReportLines();
            List<IList<string>> pages = report.Profile == FirmwareProfile.Version153
                ? CreateLegacyPagePlaceholders()
                : SplitPages(lines);
            List<byte[]> objects = new List<byte[]>();
            Encoding textEncoding = Encoding.GetEncoding(1252);

            int firstPageObject = 5;
            StringBuilder pageReferences = new StringBuilder();
            for (int page = 0; page < pages.Count; page++)
                pageReferences.AppendFormat("{0} 0 R ", firstPageObject + (page * 2));

            objects.Add(Ascii("<< /Type /Catalog /Pages 2 0 R >>"));
            objects.Add(Ascii(string.Format(
                "<< /Type /Pages /Kids [{0}] /Count {1} >>",
                pageReferences,
                pages.Count)));
            objects.Add(Ascii(
                "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica /Encoding /WinAnsiEncoding >>"));
            objects.Add(Ascii(
                "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica-Bold /Encoding /WinAnsiEncoding >>"));

            for (int page = 0; page < pages.Count; page++)
            {
                int pageObject = firstPageObject + (page * 2);
                int contentObject = pageObject + 1;

                objects.Add(Ascii(string.Format(
                    "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 595 842] " +
                    "/Resources << /Font << /F1 3 0 R /F2 4 0 R >> >> /Contents {0} 0 R >>",
                    contentObject)));

                byte[] content = report.Profile == FirmwareProfile.Version153
                    ? BuildLegacyPageContent(report, page, textEncoding)
                    : BuildPageContent(pages[page], page + 1, pages.Count, textEncoding);
                byte[] header = Ascii(string.Format("<< /Length {0} >>\nstream\n", content.Length));
                byte[] footer = Ascii("\nendstream");
                byte[] streamObject = new byte[header.Length + content.Length + footer.Length];
                Buffer.BlockCopy(header, 0, streamObject, 0, header.Length);
                Buffer.BlockCopy(content, 0, streamObject, header.Length, content.Length);
                Buffer.BlockCopy(footer, 0, streamObject, header.Length + content.Length, footer.Length);
                objects.Add(streamObject);
            }

            using (MemoryStream output = new MemoryStream())
            {
                Write(output, Ascii("%PDF-1.4\n%\xE2\xE3\xCF\xD3\n"));
                List<long> offsets = new List<long>();

                for (int index = 0; index < objects.Count; index++)
                {
                    offsets.Add(output.Position);
                    Write(output, Ascii(string.Format("{0} 0 obj\n", index + 1)));
                    Write(output, objects[index]);
                    Write(output, Ascii("\nendobj\n"));
                }

                long xrefOffset = output.Position;
                Write(output, Ascii(string.Format("xref\n0 {0}\n", objects.Count + 1)));
                Write(output, Ascii("0000000000 65535 f \n"));
                foreach (long offset in offsets)
                    Write(output, Ascii(string.Format("{0:0000000000} 00000 n \n", offset)));

                Write(output, Ascii(string.Format(
                    "trailer\n<< /Size {0} /Root 1 0 R >>\nstartxref\n{1}\n%%EOF\n",
                    objects.Count + 1,
                    xrefOffset)));

                File.WriteAllBytes(path, output.ToArray());
            }
        }

        private static List<IList<string>> CreateLegacyPagePlaceholders()
        {
            List<IList<string>> pages = new List<IList<string>>();
            pages.Add(new List<string>());
            pages.Add(new List<string>());
            return pages;
        }

        private static byte[] BuildLegacyPageContent(
            EepromReport report, int pageIndex, Encoding encoding)
        {
            StringBuilder builder = new StringBuilder();

            if (pageIndex == 0)
            {
                AppendText(builder, "F2", 13, 150, 790,
                    "Inovacao em Mecanizacao Agricola CEIFA Ltda.");
                AppendText(builder, "F2", 13, 185, 770,
                    "Gerenciador de Colheita do Cafe v.1.5.3");
                AppendText(builder, "F1", 11, 220, 740,
                    "Relatorio de Avaliacoes - " + report.LastDate);
            }

            int firstPlot = pageIndex * 6;
            int[] yPositions = { 690, 470, 250 };

            for (int localPlot = 0; localPlot < 6; localPlot++)
            {
                PlotRecord plot = report.Plots[firstPlot + localPlot];
                int column = localPlot / 3;
                int row = localPlot % 3;
                int x = column == 0 ? 55 : 315;
                int y = yPositions[row];
                IList<string> plotLines = BuildLegacyPlotLines(plot);

                for (int line = 0; line < plotLines.Count; line++)
                    AppendText(builder, "F1", 9, x, y - (line * 12), plotLines[line]);
            }

            AppendText(builder, "F1", 8, 290, 815, (pageIndex + 1).ToString());
            return encoding.GetBytes(builder.ToString());
        }

        private static IList<string> BuildLegacyPlotLines(PlotRecord plot)
        {
            List<string> lines = new List<string>();
            lines.Add(string.Format("Gleba: {0} -", plot.Number));

            if (!plot.Valid)
            {
                lines.Add("Nenhuma Avaliacao Registrada!");
                return lines;
            }

            lines.Add("Data: " + plot.Date);
            lines.Add("Variedade: " + plot.Variety);
            lines.Add(string.Format(
                System.Globalization.CultureInfo.GetCultureInfo("pt-BR"),
                "Espacamento entre Pes: {0:0.0} metros", plot.PlantSpacingMeters));
            lines.Add(string.Format(
                System.Globalization.CultureInfo.GetCultureInfo("pt-BR"),
                "Espacamento entre Ruas: {0:0.0} metros", plot.RowSpacingMeters));
            lines.Add(string.Format(
                System.Globalization.CultureInfo.GetCultureInfo("pt-BR"),
                "Altura da Planta: {0:0.0} metros", plot.PlantHeightMeters));
            lines.Add(string.Format("Plantas / Ramos por Cova: {0} ramos / cova",
                plot.BranchesPerPlant));
            lines.Add(string.Format("Porcentagem de Fruto Cereja: {0} %", plot.CherryPercent));
            lines.Add(string.Format("Porcentagem de Fruto Verde: {0} %", plot.GreenPercent));
            lines.Add("Colhedora: " + plot.Harvester);
            lines.Add(string.Format(
                System.Globalization.CultureInfo.GetCultureInfo("pt-BR"),
                "Carga Pendente: {0:0.0} L/cova", plot.PendingLoad));
            lines.Add(string.Format("Produtividade: {0} sacas/ha", plot.Productivity));
            lines.Add(string.Format(
                System.Globalization.CultureInfo.GetCultureInfo("pt-BR"),
                "Forca Media do Fruto Verde: {0:0.00} N", plot.GreenForce / 100.0));
            lines.Add(string.Format(
                System.Globalization.CultureInfo.GetCultureInfo("pt-BR"),
                "Forca Media do Fruto Cereja: {0:0.00} N", plot.CherryForce / 100.0));
            lines.Add("Diagnostico: " + plot.DiagnosisDescription);
            lines.Add(string.Format("Vibracao: {0} RPM", plot.Vibration));
            lines.Add(string.Format("Velocidade: {0} m/h", plot.SpeedMetersPerHour));
            return lines;
        }

        private static void AppendText(
            StringBuilder builder, string font, int size, int x, int y, string value)
        {
            builder.Append("BT\n/");
            builder.Append(font);
            builder.Append(" ");
            builder.Append(size);
            builder.Append(" Tf\n");
            builder.Append(x);
            builder.Append(" ");
            builder.Append(y);
            builder.Append(" Td\n(");
            builder.Append(EscapePdfText(value));
            builder.Append(") Tj\nET\n");
        }

        private static List<IList<string>> SplitPages(IList<string> lines)
        {
            List<IList<string>> pages = new List<IList<string>>();
            List<string> current = new List<string>();

            foreach (string line in lines)
            {
                if (current.Count == LinesPerPage)
                {
                    pages.Add(current);
                    current = new List<string>();
                }
                current.Add(line);
            }

            if (current.Count > 0)
                pages.Add(current);

            return pages;
        }

        private static byte[] BuildPageContent(
            IList<string> lines, int pageNumber, int pageCount, Encoding encoding)
        {
            StringBuilder builder = new StringBuilder();
            int firstLine = 0;

            if (pageNumber == 1 && lines.Count > 0)
            {
                builder.Append("BT\n/F2 15 Tf\n40 805 Td\n(");
                builder.Append(EscapePdfText(lines[0]));
                builder.Append(") Tj\nET\n");
                firstLine = 1;
            }

            builder.Append("BT\n/F1 10 Tf\n40 780 Td\n14 TL\n");

            for (int index = firstLine; index < lines.Count; index++)
            {
                string line = lines[index];
                if (line.StartsWith("GLEBA ", StringComparison.Ordinal))
                    builder.Append("/F2 11 Tf\n");
                else
                    builder.Append("/F1 10 Tf\n");

                builder.Append("(");
                builder.Append(EscapePdfText(line));
                builder.Append(") Tj\nT*\n");
            }

            builder.Append("ET\n");
            builder.Append("BT\n/F1 8 Tf\n260 24 Td\n(");
            builder.Append(string.Format("Pagina {0} de {1}", pageNumber, pageCount));
            builder.Append(") Tj\nET");
            return encoding.GetBytes(builder.ToString());
        }

        private static string EscapePdfText(string value)
        {
            return value
                .Replace("\\", "\\\\")
                .Replace("(", "\\(")
                .Replace(")", "\\)");
        }

        private static byte[] Ascii(string value)
        {
            return Encoding.GetEncoding(1252).GetBytes(value);
        }

        private static void Write(Stream stream, byte[] value)
        {
            stream.Write(value, 0, value.Length);
        }
    }
}
