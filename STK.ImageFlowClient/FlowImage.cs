using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Timers;
using System.Windows.Media.Imaging;
using System.Xml.Linq;

namespace STK.ImageFlowClient
{
    public class FlowImage
    {
        public enum rating
        {
            Other,
            Explicit,
            Safe,
            Questionable
        }

        private readonly string sampleURI;
        private readonly Uri downloadURI;
        private Image image = null;
        private readonly string fileName;
        private readonly string savePath;

        public string ID { get; private set; }
        private int idInt = -1;
        public int IdInt
        {
            get
            {
                if (idInt == -1) int.TryParse(ID, out idInt);
                return idInt;
            }
        }
        public rating Rating { get; private set; }
        public Uri Lnk { get; private set; }
        public List<string> Tags { get; private set; }

        ImageFlowClient Parent { get; set; }

        public Image Image
        {
            get
            {
                Prepare();
                if (prep != null && !prep.IsCompleted)
                    prep.Wait();
                prep = null;
                return image;

            }
        }

        public FlowImage(XElement descr, ImageFlowClient parent)
        {
            Tags = new List<string>();

            Parent = parent;
            ID = descr.Attribute(XName.Get("id")).Value;
            string sourcelnk = descr.Attribute(XName.Get("source")).Value;
            if (sourcelnk.CompareTo(string.Empty) != 0 && Uri.IsWellFormedUriString(sourcelnk, UriKind.Absolute))
                Lnk = new Uri(sourcelnk);

            Tags.AddRange(descr.Attribute(XName.Get("tags")).Value.Split(' '));

            sampleURI = descr.Attribute(XName.Get("sample_url")).Value;

            if (descr.Attribute(XName.Get("jpeg_url")).Value.CompareTo(string.Empty) == 0)
                downloadURI = new Uri(descr.Attribute(XName.Get("jpeg_url")).Value);
            else
                downloadURI = new Uri(descr.Attribute(XName.Get("file_url")).Value);

            string r = descr.Attribute(XName.Get("rating")).Value;

            if (r.ToLower().CompareTo("s") == 0)
                Rating |= FlowImage.rating.Safe;
            else if (r.ToLower().CompareTo("e") == 0)
                Rating |= FlowImage.rating.Explicit;
            else if (r.ToLower().CompareTo("q") == 0)
                Rating |= FlowImage.rating.Questionable;
            else
                Rating |= FlowImage.rating.Other;


            fileName = Path.GetInvalidFileNameChars().Aggregate(
                            System.IO.Path.GetFileName(Path.GetInvalidPathChars().Aggregate(downloadURI.LocalPath, (current, c) => current.Replace(c.ToString(), string.Empty)))
                        , (current, c) => current.Replace(c.ToString(), string.Empty));
            savePath = Path.Combine(Parent.CacheLocation.ToString(), fileName);
        }

        Task prep = null;
        object prepLock = new object();
        internal void Prepare()
        {
            lock (prepLock)
            {
                if (prep == null && image == null)
                {
                    prep = Task.Run(() =>
                    {
                        try
                        {
#if DEBUG
                            System.Threading.Thread.CurrentThread.Name = "Image load thread";
#endif

                            if (IsDownloaded)
                            {
                                image = Image.FromFile(savePath);
                            }
                            else
                            {
                                image = Image.FromStream(
                                            new MemoryStream(
                                                new WebClient().DownloadData(sampleURI)
                                            )
                                        );
                            }
                        }
                        catch { }
                    });

                }
            }
        }

        internal void Deletable()
        {
            lock (prepLock)
            {
                if (image != null)
                {
                    image.Dispose();
                    image = null;
                    prep = null;
                }
            }
        }

        internal void Save()
        {


            if (!IsDownloaded)
            {
                new WebClient().DownloadFileAsync(
                    downloadURI,
                    savePath);
            }
        }

        public bool IsDownloaded
        {
            get
            {
                return new FileInfo(savePath).Exists;
            }
        }
    }
}
