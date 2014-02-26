using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace STK.ImageFlowClient
{
    public class ImageFlowClient : IDisposable
    {
        private const int Limit = 100;

        private readonly List<FlowImage> DB = new List<FlowImage>();
        private readonly BackgroundWorker imgLoaderDB;

        private Uri source;
        private List<string> tags = new List<string>();

        public IEnumerable<string> Tags { get { return tags; } }
        public int DbCount { get { return DB.Count; } }
        public int DbMaxCount { get; private set; }
        public bool IsInit { get; private set; }

        public bool EnableExplicit { get; set; }
        public bool EnableQuestionable { get; set; }
        public bool EnableSafe { get; set; }
        public bool EnableOther { get; set; }

        public DirectoryInfo CacheLocation { get; set; }

        public ImageFlowClient(DirectoryInfo cacheLocation)
        {
            DbMaxCount = -1;
            IsInit = false;

            CacheLocation = cacheLocation;

            imgLoaderDB = new BackgroundWorker();
            imgLoaderDB.DoWork += imgLoaderDB_DoWork;
            imgLoaderDB.WorkerSupportsCancellation = true;

            EnableExplicit = EnableQuestionable = EnableOther = false;
            EnableSafe = true;


        }

        public void Dispose()
        { imgLoaderDB.CancelAsync(); }

        public Uri Source
        {
            get { return source; }
            set
            {
                IsInit = false;
                source = value;
            }
        }

        public void AddTag(params string[] tag)
        {
            IsInit = false;
            foreach (string t in tag)
                if (!tags.Contains(t) && t.CompareTo(string.Empty) != 0)
                {
                    tags.Add(t);
                }
        }
        public void ClearTags()
        {
            IsInit = false;
            tags.Clear();
        }
        public void RemoveTag(params string[] tag)
        {
            IsInit = false;
            foreach (string t in tag)
                if (tags.Contains(t))
                {
                    tags.Remove(t);
                }
        }
        object dbLoadLock = new object();
        public void Init(string goToId)
        {


            UriBuilder ub = new UriBuilder(source.Host);
            var tagslist = "";
            foreach (var tag in tags)
                tagslist += tag.Replace(" ", "_") + "+";

            ub.Query += "tags=" + tagslist;
            ub.Path += "post.xml";

            source = ub.Uri;

            lock (dbLoadLock)
            {
                Index = 0;
                DbMaxCount = -1;
                DB.Clear();
                IsInit = true;

                if (goToId.CompareTo(string.Empty) == 0)
                    loadImgDB();
                else
                {
                    GoToID(goToId);
                }
                while (imgLoaderDB.CancellationPending)
                    Thread.Sleep(250);
            }
            if (!imgLoaderDB.IsBusy)
                imgLoaderDB.RunWorkerAsync();

        }

        internal void GoToID(string id)
        {
            if (string.Empty.CompareTo(id) == 0)
            {
                Index = 0;
            }
            else
            {
                int searchindex = 0;
                int a = 0, b = 1;
                FlowImage c = null;

                int.TryParse(id, out a);
                bool found = false;
                lock (dbLoadLock)
                {
                    do
                    {
                        loadImgDB();
                        for (; searchindex < DbCount; searchindex++)
                        {
                            FlowImage k = DB[searchindex];
                            if (k.ID.CompareTo(id) == 0)
                            {
                                found = true;
                                break;
                            }
                            else
                            {
                                if (c == null || c.IdInt < k.IdInt)
                                {
                                    c = k;
                                }
                            }
                        }

                    } while (!found && DbCount < DbMaxCount);
                }
                if (found)
                {
                    Index = searchindex - 1;
                }
                else
                {
                    Index = b - 1;
                }
            }
        }

        private void loadImgDB()
        {
            int chunkSize = 0;
            lock (dbLoadLock)
            {
                if (IsInit && (DB.Count < DbMaxCount || DbMaxCount == -1))
                {
                    UriBuilder ub = new UriBuilder(Source);

                    ub.Query = ub.Query.Replace("?", "") + "&&limit=" + Limit + "&&page=" + ((int)(DB.Count / Limit) + 1);

                    var resultS = new WebClient().DownloadString(ub.Uri);
                    var resultXML = XDocument.Parse(resultS);

                    var root = resultXML.Element(XName.Get("posts"));

                    if (DbMaxCount == -1)
                        DbMaxCount = int.Parse(root.Attribute(XName.Get("count")).Value);

                    var posts = root.Elements(XName.Get("post"));
                    chunkSize = posts.Count();
                    foreach (var post in posts) { DB.Add(new FlowImage(post, this)); }
                }
            }
        }


        void imgLoaderDB_DoWork(object sender, DoWorkEventArgs e)
        {
#if DEBUG
            System.Threading.Thread.CurrentThread.Name = "Image DB load thread";
#endif
            while (DbCount < DbMaxCount)
            {
                if (!imgLoaderDB.CancellationPending)
                {
                    loadImgDB();
                }
                else
                    break;
            }
        }

        public int Index { get; set; }
        public FlowImage Next()
        {
            if (source == null)
                throw new ArgumentNullException("Source");

            int del = selectPrevIndex(selectPrevIndex(selectPrevIndex(Index)));
            if (DB.Count > del)
                DB[del].Deletable();
            int next = Index = selectNextIndex(Index);
            FlowImage ret = DB[next];
            for (int i = 1; i <= 5; i++)
            {
                next = selectNextIndex(next + 1);
                DB[next].Prepare();
            }

            return ret;
        }

        private int selectNextIndex(int i, int mod = 1)
        {
            FlowImage tmp;
            do
            {
                if (DbCount == 0)
                    break;
                i += mod;

                if (i >= DbCount)
                    i = 0;
                if (i < 0)
                    i = DbCount - 1;


                tmp = DB[i];
            } while (tmp != null && !(
               (tmp.Rating.CompareTo(STK.ImageFlowClient.FlowImage.rating.Safe) == 0 && EnableSafe)
            || (tmp.Rating.CompareTo(STK.ImageFlowClient.FlowImage.rating.Questionable) == 0 && EnableQuestionable)
            || (tmp.Rating.CompareTo(STK.ImageFlowClient.FlowImage.rating.Explicit) == 0 && EnableExplicit)
            || (tmp.Rating.CompareTo(STK.ImageFlowClient.FlowImage.rating.Other) == 0 && EnableOther)
                ));
            return i;
        }
        private int selectPrevIndex(int i)
        { return selectNextIndex(i, -1); }

        public FlowImage Previous()
        {
            if (source == null)
                throw new ArgumentNullException("Source");

            int prev = Index = selectPrevIndex(Index);
            FlowImage ret = DB[prev];
            return ret;
        }      
    }
}

