﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Threading;

namespace DbxEntityTracker.commands {
    class InitializeDatabaseCommand {
        public Action AllFilesFound;
        public Action AllFilesPopulated;
        public Action<string> Error;
        private EntityDbxLib _lib;

        public InitializeDatabaseCommand(EntityDbxLib lib) {
            _lib = lib;
        }

        public bool DdfFolderExists() {
            var ddfFiles = new DirectoryInfo(AppSettings.DDF_WSROOT.ToLower());
            return ddfFiles.Exists;
        }

        public bool DbxFolderExists() {
            var dbxFiles = new DirectoryInfo(AppSettings.DBX_ROOT.ToLower());
            return dbxFiles.Exists;
        }

        public void execute() {
            if (DdfFolderExists() && DbxFolderExists()) {

                var bw = new BackgroundWorker();

                bw.DoWork += (object foo, DoWorkEventArgs bar) => {
                    try {
                        _lib.init();
                        if (AllFilesFound != null)
                            AllFilesFound();
                        _lib.populate();
                    } catch (Exception ex) {
                        bar.Cancel = true;
                        reportError(ex.Message);
                    }
                };
                bw.RunWorkerCompleted += (object asdf, RunWorkerCompletedEventArgs status) => {
                    if (!status.Cancelled) {
                        if (AllFilesPopulated != null)
                            AllFilesPopulated();
                    }
                };
                bw.RunWorkerAsync();
            } else {
                var sb = new StringBuilder();
                if (!DdfFolderExists())
                    sb.AppendLine("DDF Folder does not exist");
                if (!DbxFolderExists())
                    sb.AppendLine("DBX Folder does not exist");
                reportError(sb.ToString());
            }

        }

        private void reportError(string s) {
            if (Error != null)
                Error(s);
        }
    }
}
