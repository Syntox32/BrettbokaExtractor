using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.IO;
using System.Xml;
using System.Xml.Serialization;
using System.Collections.ObjectModel;
using System.Security;
using System.Security.Cryptography;
using System.Text.RegularExpressions;

using PdfSharp;
using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;
using PdfSharp.Pdf.Security;

namespace BrettbokaExtractor
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("=========== Brettboka PDF Unlocker Thingy ===========");

            var bblocPath = Path.Combine(Environment.GetFolderPath(
                    Environment.SpecialFolder.LocalApplicationData), "Brettboka", "bbloc");
           
            if (args.Length > 0 && Directory.Exists(args[0]))
                bblocPath = args[0];

            var currentDir = Directory.GetCurrentDirectory();

            var glbFile = Path.Combine(bblocPath, "glb");
            var userId = GetUserID(glbFile);
            var usrFile = Path.Combine(bblocPath, userId.ToString(), "usr");
            var bookFolder = Path.Combine(bblocPath, userId.ToString(), "bbf");

            var user = GetUser(usrFile);
            
            Console.WriteLine("[!] App folder: @ {0}", bblocPath);
            Console.WriteLine("[|]");
            Console.WriteLine("[+] User ID: {0}", userId.ToString());
            Console.WriteLine("[+] Email:   {0}", user.Email);
            Console.WriteLine("[+] Name :   {0}", user.FullName);
            Console.WriteLine("[|]");


            Console.WriteLine("[+] Discovering books...");
            var books = GetAvailableBooks(user, bookFolder);
            Console.WriteLine("[+] Found {0} book{1}.",
                books.Count, books.Count == 1 ? "" : "s");
            Console.WriteLine("[|]");


            Console.WriteLine("[+] Unlocking...");
            Console.WriteLine("[|]");


            foreach(var book in books)
            {
                UnlockPDF(book, currentDir);
                Console.WriteLine("[|]");
            }

            Console.WriteLine("[+] Done.");
            Console.ReadKey();
        }

        static void UnlockPDF(Book book, string saveFolder)
        {
            Console.WriteLine("[-] Unlocking document: {0}", book.Title);

            try 
            {
                var pass = GeneratePDFPassword(book.ID, book.Aggr);
                SaveAndRemovePDFProtection(book, saveFolder, pass);

                Console.WriteLine("[|]    Document unlocked.");
            }
            catch (Exception ex)
            {
                Console.WriteLine("[!]    Error unlocking document: {0}", ex.Message);
            }
        }

        static void SaveAndRemovePDFProtection(Book book, string saveFolder, string password)
        {
            PdfDocument pdf = CompatiblePdfReader.Open(book.FilePath, password);

            pdf.SecuritySettings.OwnerPassword = string.Empty;
            pdf.SecuritySettings.UserPassword = string.Empty;
            pdf.SecuritySettings.DocumentSecurityLevel = PdfDocumentSecurityLevel.None;

            
            var savePath = Path.Combine(saveFolder, book.Title + ".pdf");

            var saveStream = new FileStream(savePath, FileMode.OpenOrCreate);
            pdf.Save(saveStream, true);
        }

        static List<Book> GetAvailableBooks(User user, string bookFolder)
        {
            var books = new List<Book>();
            var reg = new Regex(@"^bbf_(\d+)$", RegexOptions.None);
            var files = Directory.GetFiles(bookFolder);

            foreach(var file in files)
            {
                var name = Path.GetFileName(file);
                var match = reg.Match(name);

                if (match.Success)
                {
                    var id = Convert.ToInt32(match.Groups[1].ToString());
                    var book = GetBookWithID(user, id);
                    book.FilePath = file;
                    books.Add(book);
                }
            }

            return books;
        }

        static Book GetBookWithID(User user, int id)
        {
            Book book = null;

            foreach (var p in user.Products)
                foreach (var b in p.Books)
                    if (b.ID == id)
                        book = b;

            return book;
        }

        static int GetUserID(string globalFile)
        {
            var bytes = File.ReadAllBytes(globalFile);
            return Convert.ToInt32(Encoding.UTF8.GetString(bytes));
        }

        static User GetUser(string userFile)
        {
            var bytes = File.ReadAllBytes(userFile);
            var key = new byte[] { 58, 246, 82, 4, 10, 0, 198, 9, 40, 3, 5, 24, 5, 1, 4, 91, 3, 3, 150 };
            var scope = DataProtectionScope.CurrentUser;

            var data = Encoding.UTF8.GetString(ProtectedData.Unprotect(bytes, key, scope));

            User user = DeserializeUserXML(data);

            return user;
        }

        public static User DeserializeUserXML(string xml)
        {
            User user = null;

            using (var reader = XmlReader.Create(new StringReader(xml), new XmlReaderSettings()))
                user = (new XmlSerializer(typeof(User))).Deserialize(reader) as User;

            return user;
        }

        static string GeneratePDFPassword(int bookId, string aggr)
        {
            var str = "sldn/g";
            var strArrays = new string[] {
                "4k",
                bookId.ToString(),
                "94v$hl1s&6#i6",
                aggr,
                "nhs"
            };

            var str1 = string.Concat(strArrays);
            var lower = string.Concat(str, str1, "082kdf");
            var sHA1CryptoServiceProvider = new SHA1CryptoServiceProvider();
            
            for (int i = 0; i < 9; i++)
            {
                var bytes = Encoding.UTF8.GetBytes(lower);
                var numArray = sHA1CryptoServiceProvider.ComputeHash(bytes);
                
                lower = BitConverter.ToString(numArray).Replace("-", "").ToLower();
            }

            return lower;
        }
    }

    public class User
    {
        public int ID;
        public string ApiAccessKey = "";
        public string FullName = "";
        public string Email = "";
        public List<Bookmark> Bookmarks = new List<Bookmark>();
        public long LastSuccessfulSync = (long)1;
        public List<Product> Products = new List<Product>();

        public User() { }

        public override string ToString()
        {
            return string.Format("User: {0}: {1}", this.ID.ToString(), this.FullName);
        }
    }

    public class Bookmark
    {
        public int PageNumber { get; set; }
        public int ParentBookID { get; set; }
        public string Title { get; set; }

        public Bookmark() { }
    }

    public class Product
    {
        public int ID;
        public int Type;
        public string Title = "";
        public string Author = "";
        public string Publisher = "";
        public string Category = "";
        public long Expires = (long)1;
        public List<Book> Books = new List<Book>();

        public Product() { }

        public override string ToString()
        {
            return string.Format("Product ID: {0}: {1}", this.ID, this.Title);
        }
    }

    public class Book
    {
        public int ID;
        public string Title = "";
        public string Author = "";
        public int NumberOfPages = 1;
        public int DefaultPage = 1;
        public string Isbn = "";
        public string PublishYear = "";
        public string ProductIdentifier = "";
        public string TextDescription = "";
        public int CurrentPage = -1;
        public string Aggr = "";
        public string Degr = "";

        public string FilePath = "";

        public Book() { }

        public override string ToString()
        {
            return string.Format("Book ID: {0}: {1}", ID, Title);
        }
    }
}
