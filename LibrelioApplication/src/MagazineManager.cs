using System;
using System.IO;
using System.Collections.Generic;
using Windows.Storage;
using Windows.Storage.Streams;
using System.Threading;
using System.Threading.Tasks;
using System.Text;
using System.Net.Http;
using Windows.Data.Xml.Dom;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml.Media.Imaging;
using Windows.Networking.BackgroundTransfer;
using Windows.UI.Xaml;
using Windows.Graphics.Imaging;
using MuPDFWinRT;


namespace LibrelioApplication
{
    public class MagazineData
    {
        public string folderUrl { get; set; }
        public IRandomAccessStream stream { get; set; }
    }

    public class MagazineManager
    {
        private string _path;
        private string _name;
        private LibrelioUrl _pList = null;
        private IList<LibrelioUrl> _magazinesUrl = new List<LibrelioUrl>();
        private IList<LibrelioLocalUrl> _magazinesLocalUrl = new List<LibrelioLocalUrl>();

        private StorageFolder _folder = null;

        private XmlDocument localXml = null;

        public LibrelioUrl PLIST { get { return _pList; } }
        public IList<LibrelioUrl> MagazineUrl { get { return _magazinesUrl; } }
        public IList<LibrelioLocalUrl> MagazineLocalUrl { get { return _magazinesLocalUrl; } }

        public string StatusText { get; set; }

        IList<string> links = new List<string>();


        public MagazineManager(string name)
        {
            this._path = "";
            this._name = name;
            StatusText = "";
        }

        /// <summary>
        /// You can download .plist, .pdf, and different assets
        /// </summary>
        /// <param name="path">The path to the magazine directory</param>
        /// <param name="name">Name of collection</param>
        public MagazineManager(string path, string name)
        {
            this._path = path;
            this._name = name;
            StatusText = "";

            _pList = new LibrelioUrl(0, path, name + ".plist");
        }

        public async Task LoadPLISTAsync()
        {
            _folder = await KnownFolders.DocumentsLibrary.CreateFolderAsync(_name, CreationCollisionOption.OpenIfExists);
            await LoadLocalMetadata();

            var settings = new XmlLoadSettings();
            settings.ProhibitDtd = false;
            settings.ValidateOnParse = true;
            settings.ResolveExternals = true;

            try
            {
                var xml = await XmlDocument.LoadFromUriAsync(new Uri(this._pList.AbsoluteUrl), settings);
                await ReadPList(xml);
            }
            catch
            {
                throw new Exception("Unable to download plist");
            }
        }

        public async Task LoadLocalMagazineList()
        {
            _folder = await KnownFolders.DocumentsLibrary.CreateFolderAsync(_name, CreationCollisionOption.OpenIfExists);
            await LoadLocalMetadata();
        }

        public async Task<BitmapSource> DownloadThumbnailAsync(LibrelioUrl magUrl)
        {
            String s = ".";
            if (magUrl.FullName.Contains("_"))
            {
                s = "_.";
            }
            var pos = magUrl.AbsoluteUrl.LastIndexOf(s);
            var url = magUrl.AbsoluteUrl.Substring(0, pos) + ".png";
            var stream = await DownloadManager.DownloadFileAsync(url);

            var bitmap = new BitmapImage();
            try
            {
                await bitmap.SetSourceAsync(stream);
            }
            catch
            {
                bitmap = null;
            }
            return bitmap;
        }

        public async Task<BitmapSource> DownloadThumbnailAsync(LibrelioUrl magUrl, StorageFolder folder)
        {
            string s = ".";
            if (magUrl.FullName.Contains("_."))
            {
                s = "_.";
            }
            var pos = magUrl.AbsoluteUrl.LastIndexOf(s);
            var url = magUrl.AbsoluteUrl.Substring(0, pos) + ".png";
            var stream = await DownloadManager.DownloadFileAsync(url);

            await DownloadManager.StoreToFolderAsync(magUrl.FullName.Replace(s + "pdf", ".png"), folder, stream);

            var bitmap = new BitmapImage();
            try
            {
                await bitmap.SetSourceAsync(stream);
            }
            catch
            {
                bitmap = null;
            }
            return bitmap;
        }

        public async Task<BitmapSource> DownloadThumbnailAsync(LibrelioLocalUrl magUrl, StorageFolder folder)
        {
            string s = ".";
            if (magUrl.FullName.Contains("_."))
            {
                s = "_.";
            }
            var pos = magUrl.Url.LastIndexOf(s);
            var url = magUrl.Url.Substring(0, pos) + ".png";
            var stream = await DownloadManager.DownloadFileAsync(url);

            await DownloadManager.StoreToFolderAsync(magUrl.FullName.Replace(s + "pdf", ".png"), folder, stream);

            var bitmap = new BitmapImage();
            try
            {
                await bitmap.SetSourceAsync(stream);
            }
            catch
            {
                bitmap = null;
            }
            return bitmap;
        }

        public async Task<IRandomAccessStream> DownloadMagazineAsync(LibrelioUrl magUrl, StorageFolder folder, IProgress<int> progress = null, CancellationToken cancelToken = default(CancellationToken))
        {
            StatusText = "Download in progress";

            var stream = await DownloadPDFAsync(magUrl, folder, progress, cancelToken);

            await GetUrlsFromPDF(stream);

            StatusText = "Downloading 2/" + (links.Count+1);
            var url = DownloadManager.ConvertToLocalUrl(magUrl, folder);

            if (url != null)
            {
                await DownloadPDFAssetsAsync(url, links, progress, cancelToken);
            }

            StatusText = "Done";

            return stream;
        }

        public async Task<IRandomAccessStream> DownloadMagazineAsync(LibrelioLocalUrl magUrl, StorageFolder folder, IProgress<int> progress = null, CancellationToken cancelToken = default(CancellationToken))
        {
            StatusText = "Download in progress";

            var stream = await DownloadPDFAsync(magUrl, folder, progress, cancelToken);

            await GetUrlsFromPDF(stream);

            StatusText = "Downloading 2/" + (links.Count + 1);
            magUrl.FolderPath = folder.Path + "\\";
            //var url = DownloadManager.ConvertToLocalUrl(magUrl, folder);

            if (magUrl != null)
            {
                await DownloadPDFAssetsAsync(magUrl, links, progress, cancelToken);
            }

            StatusText = "Done";

            return stream;
        }

        public async Task<IRandomAccessStream> DownloadMagazineAsync(LibrelioUrl magUrl, string redirectUrl, StorageFolder folder, IProgress<int> progress = null, CancellationToken cancelToken = default(CancellationToken))
        {
            StatusText = "Download in progress";

            var tmpUrl = magUrl.AbsoluteUrl;
            magUrl.AbsoluteUrl = redirectUrl;
            var stream = await DownloadPDFAsync(magUrl, folder, progress, cancelToken);
            magUrl.AbsoluteUrl = tmpUrl;

            await GetUrlsFromPDF(stream);

            StatusText = "Downloading 2/" + (links.Count + 1);
            var url = DownloadManager.ConvertToLocalUrl(magUrl, folder);

            if (url != null)
            {
                await DownloadPDFAssetsAsync(url, links, progress, cancelToken);
            }

            StatusText = "Done";

            return stream;
        }

        public async Task<IRandomAccessStream> DownloadMagazineAsync(LibrelioLocalUrl magUrl, string redirectUrl, StorageFolder folder, IProgress<int> progress = null, CancellationToken cancelToken = default(CancellationToken))
        {
            StatusText = "Download in progress";

            var tmpUrl = magUrl.Url;
            magUrl.Url = redirectUrl;
            var stream = await DownloadPDFAsync(magUrl, folder, progress, cancelToken);
            magUrl.Url = tmpUrl;

            await GetUrlsFromPDF(stream);

            StatusText = "Downloading 2/" + (links.Count + 1);

            if (magUrl != null)
            {
                await DownloadPDFAssetsAsync(magUrl, links, progress, cancelToken);
            }

            StatusText = "Done";

            return stream;
        }

        public LibrelioLocalUrl FindInMetadata(LibrelioUrl url)
        {
            return DownloadManager.FindInMetadata(url, localXml);
        }

        public async Task<IRandomAccessStream> DownloadPDFAsync(LibrelioUrl magUrl, StorageFolder folder, IProgress<int> progress = null, CancellationToken cancelToken = default(CancellationToken))
        {
            HttpClient client = new HttpClient();

            HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, magUrl.AbsoluteUrl);

            int read = 0;
            int offset = 0;
            byte[] responseBuffer = new byte[1024];

            var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancelToken);
            response.EnsureSuccessStatusCode();

            var length = response.Content.Headers.ContentLength;

            cancelToken.ThrowIfCancellationRequested();

            var stream = new InMemoryRandomAccessStream();

            using (var responseStream = await response.Content.ReadAsStreamAsync())
            {
                do
                {
                    cancelToken.ThrowIfCancellationRequested();

                    read = await responseStream.ReadAsync(responseBuffer, 0, responseBuffer.Length);

                    cancelToken.ThrowIfCancellationRequested();

                    await stream.AsStream().WriteAsync(responseBuffer, 0, read);

                    offset += read;
                    uint val = (uint)(offset * 100 / length);
                    if (val >= 100) val = 99;
                    if (val <= 0) val = 1;
                    progress.Report((int)val);
                }
                while (read != 0);
            }

            progress.Report(100);

            await stream.FlushAsync();

            //var folder = await AddMagazineFolderStructure(magUrl);
            //var folder = await StorageFolder.GetFolderFromPathAsync(folderUrl);
            var file = await folder.CreateFileAsync(magUrl.FullName, CreationCollisionOption.ReplaceExisting);

            using (var protectedStream = await DownloadManager.ProtectPDFStream(stream))
            using (var fileStream = await file.OpenAsync(Windows.Storage.FileAccessMode.ReadWrite))
            //using (var unprotectedStream = await DownloadManager.UnprotectPDFStream(protectedStream))
            {

                await RandomAccessStream.CopyAsync(protectedStream, fileStream.GetOutputStreamAt(0));

                await fileStream.FlushAsync();
            }
            var pdfStream = new MagazineData();
            pdfStream.folderUrl = folder.Path + "\\";
            pdfStream.stream = stream;
            //var fileHandle =
            //    await Windows.ApplicationModel.Package.Current.InstalledLocation.GetFileAsync(@"Assets\test\testmagazine.pdf");

            //pdfStream.folderUrl = "C:\\Users\\Dorin\\Documents\\Magazines\\wind_355\\";
            //pdfStream.stream = await fileHandle.OpenReadAsync();

            return pdfStream.stream;
        }

        public async Task<IRandomAccessStream> DownloadPDFAsync(LibrelioLocalUrl magUrl, StorageFolder folder, IProgress<int> progress = null, CancellationToken cancelToken = default(CancellationToken))
        {
            HttpClient client = new HttpClient();

            HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, magUrl.Url);

            int read = 0;
            int offset = 0;
            byte[] responseBuffer = new byte[1024];

            var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancelToken);
            response.EnsureSuccessStatusCode();

            var length = response.Content.Headers.ContentLength;

            cancelToken.ThrowIfCancellationRequested();

            var stream = new InMemoryRandomAccessStream();

            using (var responseStream = await response.Content.ReadAsStreamAsync())
            {
                do
                {
                    cancelToken.ThrowIfCancellationRequested();

                    read = await responseStream.ReadAsync(responseBuffer, 0, responseBuffer.Length);

                    cancelToken.ThrowIfCancellationRequested();

                    await stream.AsStream().WriteAsync(responseBuffer, 0, read);

                    offset += read;
                    uint val = (uint)(offset * 100 / length);
                    if (val >= 100) val = 99;
                    if (val <= 0) val = 1;
                    progress.Report((int)val);
                }
                while (read != 0);
            }

            progress.Report(100);

            await stream.FlushAsync();

            //var folder = await AddMagazineFolderStructure(magUrl);
            //var folder = await StorageFolder.GetFolderFromPathAsync(folderUrl);
            var file = await folder.CreateFileAsync(magUrl.FullName, CreationCollisionOption.ReplaceExisting);

            using (var protectedStream = await DownloadManager.ProtectPDFStream(stream))
            using (var fileStream = await file.OpenAsync(Windows.Storage.FileAccessMode.ReadWrite))
            //using (var unprotectedStream = await DownloadManager.UnprotectPDFStream(protectedStream))
            {

                await RandomAccessStream.CopyAsync(protectedStream, fileStream.GetOutputStreamAt(0));

                await fileStream.FlushAsync();
            }
            var pdfStream = new MagazineData();
            pdfStream.folderUrl = folder.Path + "\\";
            pdfStream.stream = stream;
            //var fileHandle =
            //    await Windows.ApplicationModel.Package.Current.InstalledLocation.GetFileAsync(@"Assets\test\testmagazine.pdf");

            //pdfStream.folderUrl = "C:\\Users\\Dorin\\Documents\\Magazines\\wind_355\\";
            //pdfStream.stream = await fileHandle.OpenReadAsync();

            return pdfStream.stream;
        }

        public async Task<StorageFolder> AddMagazineFolderStructure(LibrelioUrl magUrl)
        {
            var currentFolder = _folder;

            var relUrl = magUrl.RelativeUrl;
            var strs = relUrl.Split('/');

            for (int i = 0; i < strs.Length - 1; i++)
            {
                var folder = strs[i];
                if (folder != "")
                {
                    try
                    {
                        var fld = await currentFolder.CreateFolderAsync(folder, CreationCollisionOption.OpenIfExists);
                        currentFolder = fld;
                    }
                    catch
                    {
                        return null;
                    }
                }
            }

            if (currentFolder != _folder)
            {
            //    var magLocal = new LibrelioLocalUrl(magUrl.Title, magUrl.Subtitle, currentFolder.Path + "\\", 
            //                                        magUrl.FullName, magUrl.AbsoluteUrl, magUrl.RelativeUrl);

            //    if (!UpdataLocalUrl(magLocal))
            //        _magazinesLocalUrl.Add(magLocal);

            //    await AddUpdateMetadataEntry(magLocal);

                return currentFolder;
            }

            return null;
        }

        public async Task MarkAsDownloaded(LibrelioUrl magUrl, StorageFolder currentFolder)
        {
            if (currentFolder != _folder)
            {
                var magLocal = new LibrelioLocalUrl(magUrl.Index, magUrl.Title, magUrl.Subtitle, currentFolder.Path + "\\",
                                                    magUrl.FullName, magUrl.AbsoluteUrl, magUrl.RelativeUrl);

                if (!UpdataLocalUrl(magLocal))
                    _magazinesLocalUrl.Add(magLocal);

                await AddUpdateMetadataEntry(magLocal);
            }
        }

        public async Task MarkAsDownloaded(LibrelioLocalUrl magUrl, StorageFolder currentFolder)
        {
            if (currentFolder != _folder)
            {
                if (!UpdataLocalUrl(magUrl))
                    _magazinesLocalUrl.Add(magUrl);

                await AddUpdateMetadataEntry(magUrl);
            }
        }

        public async Task<StorageFolder> AddMagazineFolderStructure(LibrelioLocalUrl magLocal)
        {
            var currentFolder = _folder;

            var relUrl = magLocal.RelativePath;
            var strs = relUrl.Split('/');

            for (int i = 0; i < strs.Length - 1; i++)
            {
                var folder = strs[i];
                if (folder != "")
                {
                    try
                    {
                        var fld = await currentFolder.CreateFolderAsync(folder, CreationCollisionOption.OpenIfExists);
                        currentFolder = fld;
                    }
                    catch
                    {
                        return null;
                    }
                }
            }

            if (currentFolder != _folder)
            {
                //    var magLocal = new LibrelioLocalUrl(magUrl.Title, magUrl.Subtitle, currentFolder.Path + "\\", 
                //                                        magUrl.FullName, magUrl.AbsoluteUrl, magUrl.RelativeUrl);

                //    if (!UpdataLocalUrl(magLocal))
                //        _magazinesLocalUrl.Add(magLocal);

                //    await AddUpdateMetadataEntry(magLocal);

                return currentFolder;
            }

            return null;
        }

        public async Task AddUpdateMetadataEntry(LibrelioLocalUrl magLocal)
        {
            if(localXml == null)
                await LoadLocalMetadata();

            string xpath = "/root/mag[url='" + magLocal.Url + "']";
            var mags = localXml.SelectNodes(xpath);

            if (mags.Count > 0)
            {
                mags[0].SelectNodes("index")[0].InnerText = magLocal.Index.ToString();
                mags[0].SelectNodes("title")[0].InnerText = magLocal.Title;
                mags[0].SelectNodes("subtitle")[0].InnerText = magLocal.Subtitle;
                if (magLocal.FolderPath != "ND")
                {
                    mags[0].SelectNodes("path")[0].InnerText = magLocal.FolderPath + magLocal.FullName;
                    mags[0].SelectNodes("metadata")[0].InnerText = magLocal.FolderPath + magLocal.MetadataName;
                }
                else
                {
                    mags[0].SelectNodes("path")[0].InnerText = "ND";
                    mags[0].SelectNodes("metadata")[0].InnerText = "ND";
                }
                mags[0].SelectNodes("url")[0].InnerText = magLocal.Url;
                mags[0].SelectNodes("relPath")[0].InnerText = magLocal.RelativePath;
            }
            else
            {
                var mag = localXml.CreateElement("mag");

                var index = localXml.CreateElement("index");
                index.InnerText = magLocal.Index.ToString();
                mag.AppendChild(index);

                var title = localXml.CreateElement("title");
                title.InnerText = magLocal.Title;
                mag.AppendChild(title);

                var subtitle = localXml.CreateElement("subtitle");
                subtitle.InnerText = magLocal.Subtitle;
                mag.AppendChild(subtitle);

                var path = localXml.CreateElement("path");
                if (magLocal.FolderPath != "ND")
                    path.InnerText = magLocal.FolderPath + magLocal.FullName;
                else
                    path.InnerText = "ND";
                mag.AppendChild(path);

                var metadata = localXml.CreateElement("metadata");
                if (magLocal.FolderPath != "ND")
                    metadata.InnerText = magLocal.FolderPath + magLocal.MetadataName;
                else
                    metadata.InnerText = "ND";
                mag.AppendChild(metadata);

                var url = localXml.CreateElement("url");
                url.InnerText = magLocal.Url;
                mag.AppendChild(url);

                var relPath = localXml.CreateElement("relPath");
                relPath.InnerText = magLocal.RelativePath;
                mag.AppendChild(relPath);

                localXml.GetElementsByTagName("root")[0].AppendChild(mag);
            }

            var xmlfile = await _folder.CreateFileAsync("magazines.metadata", CreationCollisionOption.OpenIfExists);
            await localXml.SaveToFileAsync(xmlfile);
        }

        public IList<LibrelioUrl> GetAssets(string magazineName)
        {
            return null;
        }

        private async Task ReadPList(XmlDocument plist)
        {
            var items = plist.SelectNodes("/plist/array/dict");
            for (int i = 0; i < items.Count; i++)
            {
                var dict = items[i];
                LibrelioUrl url = null;
                string tite = "";
                string subtitle = "";

                foreach (var key in dict.SelectNodes("key"))
                {
                    if (key.InnerText == "FileName")
                    {
                        var relUrl = GetValue(key);

                        if (relUrl != "")
                            url = new LibrelioUrl(i, this._path, relUrl);
                    }
                    else if (key.InnerText == "Title")
                    {
                        tite = GetValue(key);
                    }
                    else if (key.InnerText == "Subtitle")
                    {
                        subtitle = GetValue(key);
                    }
                }

                if (url != null && tite != "")
                    url.Title = tite;
                if (url != null && subtitle != "")
                    url.Subtitle = subtitle;
                if (url != null)
                    _magazinesUrl.Add(url);
            }

            await UpdateLocalMetadataFromPLIST();
        }

        private string GetValue(IXmlNode key)
        {
            var node = key;

            while (node.NextSibling != null && node.NodeName != "string")
                node = node.NextSibling;
            if (node != null)
                return node.InnerText;
            else
                return "";
        }

        private bool UpdataLocalUrl(LibrelioLocalUrl url)
        {
            foreach (var local in _magazinesLocalUrl)
            {
                if (local.FullName == url.FullName)
                {
                    var i = _magazinesLocalUrl.IndexOf(local);
                    _magazinesLocalUrl[i] = url;
                    return true;
                }
            }

            return false;
        }

        private async Task UpdateLocalMetadataFromPLIST()
        {
            foreach (var url in _magazinesUrl)
            {
                LibrelioLocalUrl local = null;
                local = DownloadManager.FindInMetadata(url, localXml);

                if (local == null)
                {
                    local = DownloadManager.ConvertToLocalUrl(url);
                    if (!UpdataLocalUrl(local))
                        _magazinesLocalUrl.Add(local);
                }

                if (!DownloadManager.IsDownloaded(local))
                    await AddUpdateMetadataEntry(local);
            }
        }

        private async Task LoadLocalMetadata()
        {
            if (localXml == null)
            {
                var file = await _folder.CreateFileAsync("magazines.metadata", CreationCollisionOption.OpenIfExists);
                try
                {
                    localXml = await XmlDocument.LoadFromFileAsync(file);
                }
                catch
                {
                    localXml = new XmlDocument();
                    var root = localXml.CreateElement("root");
                    localXml.AppendChild(root);

                    var task = localXml.SaveToFileAsync(file).AsTask();

                    return;
                }
            }

            _magazinesLocalUrl.Clear();
            var mags = localXml.SelectNodes("/root/mag");
            bool error = false;
            foreach (var mag in mags)
            {
                try
                {
                    _magazinesLocalUrl.Add(DownloadManager.GetLocalUrl(mag));
                }
                catch
                {
                    error = true;
                    break;
                }
            }

            if (error)
            {
                var file = await _folder.CreateFileAsync("magazines.metadata", CreationCollisionOption.ReplaceExisting);

                localXml = new XmlDocument();
                var root = localXml.CreateElement("root");
                localXml.AppendChild(root);

                var task = localXml.SaveToFileAsync(file).AsTask();

                _magazinesLocalUrl.Clear();
            }
        }

        private async Task GetUrlsFromPDF(IRandomAccessStream stream)
        {
            using (var dataReader = new DataReader(stream.GetInputStreamAt(0)))
            {
                uint u = await dataReader.LoadAsync((uint)stream.Size);
                IBuffer buffer = dataReader.ReadBuffer(u);

                GetPDFLinks(buffer);

                TimeSpan t = new TimeSpan(0, 0, 1);
                await Task.Delay(t);
            }
        }

        private void GetPDFLinks(IBuffer buffer)
        {
            var document = Document.Create(
                        buffer, // - file
                        DocumentType.PDF, // type
                        72 // - dpi
                      );

            var linkVistor = new LinkInfoVisitor();
            linkVistor.OnURILink += linkVistor_OnURILink;

            for (int i = 0; i < document.PageCount; i++)
            {
                var links = document.GetLinks(i);

                for (int j = 0; j < links.Count; j++)
                {
                    links[j].AcceptVisitor(linkVistor);

                }
            }
        }

        private void linkVistor_OnURILink(LinkInfoVisitor __param0, LinkInfoURI __param1)
        {
            int pos = 0;
            int pos1 = 0;
            string str = __param1.URI;
            if (str.Contains("localhost"))
            {
                if (DownloadManager.IsImage(str))
                {
                    pos = str.LastIndexOf('_');
                    if (str.Contains(".jpg") && pos > 0)
                    {
                        pos1 = str.IndexOf(".jpg");
                    }

                    if (pos >= 0 && pos1 >= 0)
                    {
                        var s = str.Substring(0, pos1);
                        var ss = s.Substring(pos + 1);
                        int x = Convert.ToInt32(ss);
                        if (x > 1 && x < 50)
                        {
                            for (int i = 1; i < x; i++)
                                links.Add(str.Replace("_" + ss + ".", "_" + Convert.ToString(i) + "."));
                        }
                    }

                    links.Add(str);
                }
                else if (DownloadManager.IsVideo(str))
                {
                    pos1 = str.IndexOf(".mp4");
                    str = str.Substring(0, pos1) + ".mp4";

                    links.Add(str);
                }
            }
        }

        private async Task<StorageFile> DownloadPDFAssetsAsync(LibrelioLocalUrl magUrl, IList<string> list, IProgress<int> progress = null, CancellationToken cancelToken = default(CancellationToken))
        {
            var folder = await StorageFolder.GetFolderFromPathAsync(magUrl.FolderPath.Substring(0, magUrl.FolderPath.Length-1));
            var file = await folder.CreateFileAsync(magUrl.MetadataName, CreationCollisionOption.ReplaceExisting);
            var xml = new XmlDocument();
            var root = xml.CreateElement("root");
            var name = xml.CreateElement("name");
            name.InnerText = magUrl.FullName;
            var date = xml.CreateElement("date");
            date.InnerText = DateTime.Today.Month.ToString() + "/" + DateTime.Today.Day.ToString() + "/" + DateTime.Today.Year.ToString();
            xml.AppendChild(root);
            root.AppendChild(name);
            root.AppendChild(date);
            await xml.SaveToFileAsync(file);
            cancelToken.ThrowIfCancellationRequested();

            for (int i = 0; i < list.Count; i++)
            {
                StatusText = "Downloading " + (i+2) + "/" + (list.Count+1);
                var url = list[i];
                cancelToken.ThrowIfCancellationRequested();

                string absLink = url;
                var pos = absLink.IndexOf('?');
                if (pos >= 0) absLink = url.Substring(0, pos);
                string fileName = absLink.Replace("http://localhost/", "");
                string linkString = "";
                linkString = folder.Path + "\\" + absLink.Replace("http://localhost/", "");
                pos = magUrl.Url.LastIndexOf('/');
                var assetUrl = magUrl.Url.Substring(0, pos + 1);
                absLink = absLink.Replace("http://localhost/", assetUrl);

                var sampleFile = await folder.CreateFileAsync(fileName, CreationCollisionOption.ReplaceExisting);

                await DownloadFileAsyncWithProgress(absLink, sampleFile, progress, cancelToken);

                var asset = xml.CreateElement("asset");
                asset.InnerText = linkString;
                root.AppendChild(asset);
                await xml.SaveToFileAsync(file);
            }

            return file;
        }

        private async Task DownloadFileAsyncWithProgress(string url, StorageFile pdfFile, IProgress<int> progress = null, CancellationToken cancelToken = default(CancellationToken))
        {
            //HttpClient client = new HttpClient();

            //HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, url);

            //int read = 0;
            //int offset = 0;
            //byte[] responseBuffer = new byte[2000];

            //var response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancelToken);
            //response.EnsureSuccessStatusCode();

            //var length = response.Content.Headers.ContentLength;

            //byte[] img = await response.Content.ReadAsByteArrayAsync();

            //cancelToken.ThrowIfCancellationRequested();

            //var stream = new InMemoryRandomAccessStream();
            //DataWriter writer = new DataWriter(stream.GetOutputStreamAt(0));

            //writer.WriteBytes(img);

            //await writer.StoreAsync();


            //var task1 = client.GetStreamAsync(new Uri(url));

            //var cancelation = new CancellationTokenSource();

            //var task2 = Task.Run(async () =>
            //{
            //    for (int i = 0; i < length; i += 9000)
            //    {
            //        await Task.Delay(1);
            //        int val = (int)(i * 100 / length);
            //        progress.Report(val);

            //        if (cancelation.Token.IsCancellationRequested)
            //        {
            //            progress.Report(99);
            //            return;
            //        }
            //    }
            //}, cancelation.Token);

            //var result = await task1;

            //cancelation.Cancel();
            //progress.Report(99);

            //await result.CopyToAsync(stream.AsStream());
            //await stream.FlushAsync();

            //await Task.Delay(670);

            //var responseStream = await response.Content.ReadAsStreamAsync();

            //do
            //{
            //    cancelToken.ThrowIfCancellationRequested();

            //    read = await responseStream.ReadAsync(responseBuffer, 0, responseBuffer.Length);

            //    cancelToken.ThrowIfCancellationRequested();

            //    await stream.AsStream().WriteAsync(responseBuffer, 0, read);

            //    offset += read;
            //    int val = (int)(offset * 100 / length);
            //    progress.Report(val);
            //}
            //while (read > 0);

            //await responseStream.FlushAsync();
            //responseStream.Dispose();
            //responseStream = null;
            //await stream.FlushAsync();

            //var protectedStream = await DownloadManager.ProtectPDFStream(stream);
            //var fileStream = await pdfFile.OpenAsync(Windows.Storage.FileAccessMode.ReadWrite);
            //using (var unprotectedStream = await DownloadManager.UnprotectPDFStream(protectedStream))

            //await RandomAccessStream.CopyAndCloseAsync(stream.GetInputStreamAt(0), fileStream.GetOutputStreamAt(0));

            //await stream.FlushAsync();
            //stream.Dispose();
            ///stream = null;
            //await protectedStream.FlushAsync();
            //protectedStream.Dispose();
            //protectedStream = null;
            //await fileStream.FlushAsync();
            //fileStream.Dispose();
            //fileStream = null;
            //pdfFile = null;

            progress.Report(0);
            BackgroundDownloader downloader = new BackgroundDownloader();
            DownloadOperation download = downloader.CreateDownload(new Uri(url), pdfFile);

            await HandleDownloadAsync(download,  true, progress, cancelToken);

            progress.Report(100);

            var stream = await download.ResultFile.OpenAsync(FileAccessMode.ReadWrite);
            var protectedStram = await DownloadManager.ProtectPDFStream(stream);
            await RandomAccessStream.CopyAndCloseAsync(protectedStram.GetInputStreamAt(0), stream.GetOutputStreamAt(0));
            await protectedStram.FlushAsync();
            await stream.FlushAsync();
            protectedStram.Dispose();
            stream.Dispose();
        }

        private async Task HandleDownloadAsync(DownloadOperation download, bool start, IProgress<int> progress = null, CancellationToken cancelToken = default(CancellationToken))
        {
            var app = Application.Current as App;

            try
            {
                // Store the download so we can pause/resume. 
                app.activeDownloads.Add(download);

                Progress<DownloadOperation> progressCallback = new Progress<DownloadOperation>((operation) =>
                {
                    if (operation.Progress.TotalBytesToReceive != 0)
                    {
                        ulong val = (ulong)(operation.Progress.BytesReceived * 100 / operation.Progress.TotalBytesToReceive);
                        progress.Report((int)val);
                    }
                });

                if (start)
                {
                    // Start the download and attach a progress handler. 
                    await download.StartAsync().AsTask(cancelToken, progressCallback);
                }
                else
                {
                    // The download was already running when the application started, re-attach the progress handler. 
                    await download.AttachAsync().AsTask(cancelToken, progressCallback);
                }
            }
            catch (TaskCanceledException)
            {
            }
            catch (Exception ex)
            {
            }
            finally
            {
                app.activeDownloads.Remove(download);
            }
        } 
    }
}