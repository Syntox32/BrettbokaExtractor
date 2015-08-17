using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.IO;
using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;

using TextSharp = iTextSharp.text.pdf;

namespace BrettbokaExtractor
{
    /// <summary>
    /// The default PDFSharp library cannot handle Iref streams, due do it being
    /// a feature implemented in a newer version of PDF. This is a workaround for
    /// opening and reading these newer PDF files.
    /// 
    /// Also modified to support passwords.
    /// 
    /// stackoverflow.com/questions/12782295/does-pdf-file-contain-iref-stream
    /// </summary>
    static public class CompatiblePdfReader
    {
        /// <summary>
        /// uses itextsharp 4.1.6 to convert any pdf to 1.4 compatible pdf, called instead of PdfReader.open
        /// </summary>
        static public PdfDocument Open(string pdfPath, string password = null)
        {
            var bytes = File.ReadAllBytes(pdfPath);
            return Open(bytes, password);
        }

        /// <summary>
        /// uses itextsharp 4.1.6 to convert any pdf to 1.4 compatible pdf, called instead of PdfReader.open
        /// </summary>
        static public PdfDocument Open(byte[] fileArray, string password = null)
        {
            return Open(new MemoryStream(fileArray), password);
        }

        /// <summary>
        /// uses itextsharp 4.1.6 to convert any pdf to 1.4 compatible pdf, called instead of PdfReader.open
        /// </summary>
        static public PdfDocument Open(MemoryStream sourceStream, string password = null)
        {
            PdfDocument outDoc;
            sourceStream.Position = 0;
            var mode = PdfDocumentOpenMode.Modify;

            try
            {
                outDoc = PdfReader.Open(sourceStream, mode);
            }
            catch (PdfReaderException)
            {
                sourceStream.Position = 0;
                var outputStream = new MemoryStream();

                var reader = password == null ?
                    new TextSharp.PdfReader(sourceStream) :
                    new TextSharp.PdfReader(sourceStream, Encoding.UTF8.GetBytes(password));

                var pdfStamper = new iTextSharp.text.pdf.PdfStamper(reader, outputStream) { FormFlattening = true };
                pdfStamper.Writer.SetPdfVersion(iTextSharp.text.pdf.PdfWriter.PDF_VERSION_1_4);
                pdfStamper.Writer.CloseStream = false;
                pdfStamper.Close();

                outDoc = PdfReader.Open(outputStream, mode);
            }

            return outDoc;
        }
    }
}
