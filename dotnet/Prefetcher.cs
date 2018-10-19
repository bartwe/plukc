using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;

namespace Compiler {
    public class Prefetcher : IDisposable {
        readonly object requested_lock = new object();
        readonly Set<string> requested = new Set<string>(StringComparer.Ordinal);
        readonly object queued_lock = new object();
        readonly Queue<string> queued = new Queue<string>();
        readonly object responses_lock = new object();
        Dictionary<string, StreamReader> responses = new Dictionary<string, StreamReader>(StringComparer.Ordinal);

        AutoResetEvent newQueued = new AutoResetEvent(false);
        AutoResetEvent newResponse = new AutoResetEvent(false);

        Thread[] workers;
        bool working = true;

        public Prefetcher(bool ignoreCase) {
            this.ignoreCase = ignoreCase;
            if (ignoreCase)
                fileexistscache = new Dictionary<string, Set<string>>(StringComparer.OrdinalIgnoreCase);
            else
                fileexistscache = new Dictionary<string, Set<string>>(StringComparer.Ordinal);
            workers = new Thread[2];
            for (var i = 0; i < workers.Length; ++i) {
                workers[i] = new Thread(Work);
                workers[i].Start();
            }
        }

        public void Dispose() {
            if (workers != null) {
                working = false;
                newQueued.Set();
                for (var i = 0; i < workers.Length; ++i)
                    workers[i].Join();
                workers = null;
            }
            if (responses != null) {
                foreach (var s in responses.Values)
                    if (s != null)
                        s.Dispose();
                responses = null;
            }
            if (newQueued != null) {
                newQueued.Close();
                newQueued = null;
            }
            if (newResponse != null) {
                newResponse.Close();
                newResponse = null;
            }
        }

        void Work() {
            while (working) {
                string pending = null;
                lock (queued_lock) {
                    if (queued.Count > 0)
                        pending = queued.Dequeue();
                }
                if (pending == null) {
                    if (working)
                        newQueued.WaitOne();
                }
                else {
                    var sr = Read(pending);
                    if (sr != null)
                        sr.Peek();
                    lock (responses_lock) {
                        responses.Add(pending, sr);
                    }
                    newResponse.Set();
                }
            }
            newQueued.Set();
        }

        public void Enqueue(string filename) {
            lock (requested_lock) {
                if (requested.Contains(filename))
                    return;
                requested.Add(filename);
            }
            lock (queued_lock) {
                queued.Enqueue(filename);
            }
            newQueued.Set();
        }

        public StreamReader Request(string filename) {
            while (true) {
                bool queued;
                lock (responses_lock) {
                    StreamReader result;
                    if (responses.TryGetValue(filename, out result)) {
                        responses.Remove(filename);
                        return result;
                    }
                }
                lock (requested_lock) {
                    queued = requested.Contains(filename);
                    if (!queued)
                        requested.Add(filename);
                }
                if (queued) {
                    newResponse.WaitOne();
                }
                else {
                    return Read(filename);
                }
            }
        }

        public string FirstFromSet(Set<string> filenames) {
            Require.True(filenames.Count > 0);
            while (true) {
                lock (responses_lock) {
                    foreach (var file in responses.Keys) {
                        if (filenames.Contains(file))
                            return file;
                    }
                }
                newResponse.WaitOne();
            }
        }

        readonly bool ignoreCase;
        readonly Dictionary<string, Set<string>> fileexistscache;
        readonly object fileexistscache_dir_locker = new object();

        bool Exists(string filename) {
            var path = Path.GetDirectoryName(filename);
            Set<string> files;
            lock (fileexistscache) {
                if (fileexistscache.TryGetValue(path, out files))
                    return files.Contains(filename);
            }
            lock (fileexistscache_dir_locker) // only do one directory request at a time, do not block the cache to the other results
            {
                lock (fileexistscache) // if we got overtaken by another thread, use their work
                {
                    if (fileexistscache.TryGetValue(path, out files))
                        return files.Contains(filename);
                }
                if (ignoreCase)
                    files = new Set<string>(StringComparer.OrdinalIgnoreCase);
                else
                    files = new Set<string>(StringComparer.Ordinal);
                string[] entries = null;
                if (Directory.Exists(path))
                    entries = Directory.GetFiles(path);
                lock (fileexistscache) {
                    if (entries != null)
                        files.AddRange(entries);
                    fileexistscache[path] = files;
                    return files.Contains(filename);
                }
            }
        }

        StreamReader Read(string filename) {
            if (Exists(filename)) {
                using (var fs = new FileStream(filename, FileMode.Open, FileAccess.Read, FileShare.ReadWrite)) {
                    var buffer = new byte[fs.Length];
                    fs.Read(buffer, 0, buffer.Length);
                    return new StreamReader(new MemoryStream(buffer, false), Encoding.UTF8, true);
                }
            }
            return null;
        }
    }
}
