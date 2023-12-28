using System;
using System.IO;
using System.Text;
using System.Collections.Generic;
using QuickFix;

namespace Matching
{
    public class DumpFile : IDisposable
    {
        private bool _disposed;
        private string _path;
        private string _name;
        StringBuilder _builder;
        private StreamWriter _dump;

        public DumpFile(String path, string name)
        {
            _disposed = false;
            _path = path;
            _name = name;
            _dump = null;
            _builder = new StringBuilder();

        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _disposed = true;
                _builder.Clear();
                _builder = null;
                _dump = null;
            }
        }

        private string GetFileName()
        {
            _builder.Clear();
            _builder.Append(_path).Append(Path.DirectorySeparatorChar);
            _builder.Append(_name).Append("_");
            _builder.Append(DateTime.Now.ToString("yyyy-MM-dd")).Append(".dump");

            return _builder.ToString();
        }

        private void OpenFile()
        {
            if (!Directory.Exists(_path))
            {
                Directory.CreateDirectory(_path);
            }

            _dump = new StreamWriter(GetFileName(), true);
        }

        private void CloseFile()
        {
            if (_dump != null)
            {
                _dump.Flush();
                _dump.Close();
                _dump.Dispose();
                _dump = null;
            }
        }

        public void Write(string msg)
        {
            OpenFile();
            _dump.WriteLine(msg);
            CloseFile();
        }

        public void RemoveFile()
        {
            string tmpFile = GetFileName() + ".tmp";
            if(File.Exists(tmpFile))
                File.Delete(tmpFile);
        }

        public bool Exists()
        {
            return File.Exists(GetFileName());
        }

        public void ReadAll(Queue<string> list)
        {
            string filename = GetFileName();
            if(File.Exists(filename))
            {
                OpenFile();

                using (StreamReader sr = new StreamReader(filename))
                {
                    string line;
                    while ((line = sr.ReadLine()) != null)
                    {
                        list.Enqueue( line);
                    }
                }
                File.Move(filename, filename + ".tmp");
                CloseFile();
            }
        }
    }
}