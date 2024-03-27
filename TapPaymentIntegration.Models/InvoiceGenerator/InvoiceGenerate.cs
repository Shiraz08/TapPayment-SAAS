//using iTextSharp.text.pdf;
//using iTextSharp.tool.xml.html;
//using iTextSharp.tool.xml.parser;
//using iTextSharp.tool.xml.pipeline.css;
//using iTextSharp.tool.xml.pipeline.end;
//using iTextSharp.tool.xml.pipeline.html;
//using iTextSharp.tool.xml;
//using System.Net;
//using iTextSharp.text;

//namespace TapPaymentIntegration.Models.InvoiceGenerator
//{
//    public class InvoiceGenerate
//    {
//        public static byte[] GeneratePDF(string body, string contentRootPath1)
//        {
//            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls | SecurityProtocolType.Tls11 | SecurityProtocolType.Tls12;
//            byte[]? bytesArray = null;
//            using (var ms = new MemoryStream())
//            {
//                var document = new Document(PageSize.A4, 0f, 150f, 0f, 0f);
//                PdfWriter writer = PdfWriter.GetInstance(document, ms);
//                document.Open();
//                using (var strReader = new StringReader(body))
//                {
//                    //Set factories
//                    HtmlPipelineContext htmlContext = new HtmlPipelineContext(null);
//                    htmlContext.SetTagFactory(Tags.GetHtmlTagProcessorFactory());
//                    //Set css
//                    ICSSResolver cssResolver = XMLWorkerHelper.GetInstance().GetDefaultCssResolver(false);
//                    cssResolver.AddCssFile(contentRootPath1, true);
//                    //Export
//                    IPipeline pipeline = new CssResolverPipeline(cssResolver, new HtmlPipeline(htmlContext, new PdfWriterPipeline(document, writer)));
//                    var worker = new XMLWorker(pipeline, true);
//                    var xmlParse = new XMLParser(true, worker);
//                    xmlParse.Parse(strReader);
//                    xmlParse.Flush();
//                }
//                document.Close();
//                bytesArray = ms.ToArray();
//            }
//            return bytesArray;
//        }
//    }
//}
