using System;
using System.Collections.Generic;
using System.Linq;
using SAP.Middleware.Connector;
using System.IO;
using System.Windows.Forms;
using System.Globalization;

namespace ManageCompositeRole
{
    public class LogEventArgs : EventArgs
    {
        public String Log { get; set; }

        public LogEventArgs(String log)
        {
            this.Log = log;
        }
    }

    public delegate void LogEventHandler(Object sender, LogEventArgs e);
    public delegate void CompleteEventHandler(Object sender, LogEventArgs e);
    public delegate void ErrorEventHandler(Object sender, LogEventArgs e);

    class ManageCompositeRole
    {
        private RfcDestination destination;
        //private Exception _ex;
        private bool connected;
        private delegate void RoleHandler(List<Tuple<String, String, String, String, String, String, String>> t_agr_agrs);
        private Dictionary<Operations, RoleHandler> doEvent;

        public event LogEventHandler OperationStatus;
        public event CompleteEventHandler CompleteStatus;
        public event ErrorEventHandler ErrorStatus;

        private bool raiseError;

        public RfcDestination Destination
        {
            get { return destination; }
        }

        public bool Connected
        {
            get { return connected; }
        }

        protected virtual void OnLog(LogEventArgs e)
        {
            LogEventHandler handler = OperationStatus;
            if (handler != null)
            {
                handler(this, e);
            }
        }

        protected virtual void OnComplete(LogEventArgs e)
        {
            CompleteEventHandler handler = CompleteStatus;
            if (handler != null)
            {
                handler(this, e);
            }
        }

        protected virtual void OnError(LogEventArgs e)
        {
            ErrorEventHandler handler = ErrorStatus;
            raiseError = true;
            if (handler != null)
            {
                handler(this, e);
            }
        }

        public ManageCompositeRole()
        {
            connected = false;
            doEvent = new Dictionary<Operations, RoleHandler>() {
                {Operations.AddRoleToComposite, DoOnCompositeRoles },
                {Operations.DelRoleToComposite, DoOnCompositeRoles },
                {Operations.NewComposite, CreateNewRoles },
                {Operations.NewSingle, CreateNewRoles },
                {Operations.CopyRole, CopyRole },
                {Operations.RenameRole, RenameRoles},
                {Operations.AddTCodes, AddTCodes},
                {Operations.DelTCodes, DeleteTCodes},
                {Operations.DeleteRole, DeleteSingleRole},
                {Operations.CompleteNewSingle, CreateCompleteSingleRole},
                {Operations.DerivateRole, CreateDerivateRole},
                {Operations.DownloadSpoolJob, DoDownloadSpooljob },
                {Operations.ExportFieldTable, GetTabFieldInfo },
                {Operations.AssignRole, UserAssignment },
                {Operations.RemoveRole, UserAssignment },
                /*{Operations.GenerateProfile, GenerateProfile}*/};
        }

        public bool Login(ProxyParameter parameter)
        {
            try
            {
                RfcConfigParameters confParam = new RfcConfigParameters();
                confParam.Add(RfcConfigParameters.Name, Guid.NewGuid().ToString());
                confParam.Add(RfcConfigParameters.AppServerHost, parameter.AppServerHost);
                confParam.Add(RfcConfigParameters.SAPRouter, parameter.SAPRouter);
                confParam.Add(RfcConfigParameters.Client, parameter.Client);
                confParam.Add(RfcConfigParameters.User, parameter.User);
                confParam.Add(RfcConfigParameters.Password, parameter.Password);
                confParam.Add(RfcConfigParameters.SystemNumber, parameter.SystemNumber);
                confParam.Add(RfcConfigParameters.Language, parameter.Language);
                confParam.Add(RfcConfigParameters.PoolSize, "1");
                confParam.Add(RfcConfigParameters.MaxPoolSize, "10");
                confParam.Add(RfcConfigParameters.IdleTimeout, "10");

                destination = RfcDestinationManager.GetDestination(confParam);
                destination.Ping();

                connected = true;
            }
            catch (Exception ex)
            {
                //Console.WriteLine(ex.Message + " " + ex.StackTrace);
                System.Diagnostics.Trace.WriteLine(ex.Message + " " + ex.StackTrace);
            }
            return connected;
        }

        public bool Disconnect()
        {
            destination = null;
            connected = false;
            return connected;
        }

        public Operations? Operation { get; set; }

        private void DoOpSingleRoleCompositeRole(String composite, String[] singleRoles)
        {
            try
            {
                RfcRepository repo = destination.Repository;
                String ops = "";
                if (this.Operation == Operations.DelRoleToComposite)
                    ops = "PRGN_RFC_DEL_AGRS_IN_COLL_AGR";
                if (this.Operation == Operations.AddRoleToComposite)
                    ops = "PRGN_RFC_ADD_AGRS_TO_COLL_AGR";

                IRfcFunction func = repo.CreateFunction(ops);
                func.SetValue("ACTIVITY_GROUP", composite);
                IRfcTable tSingleRoles = func.GetTable("ACTIVITY_GROUPS");
                foreach (String single in singleRoles)
                {
                    tSingleRoles.Append();
                    tSingleRoles.SetValue("AGR_NAME", single);
                    tSingleRoles.SetValue("TEXT", "");
                }
                func.Invoke(destination);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.WriteLine(ex.Message + " " + ex.StackTrace);
                OnError(new LogEventArgs(ex.Message));
            }
        }

        public void DoOnCompositeRoles(List<Tuple<String, String, String, String, String, String, String>> agr_agrs)
        {
            IEnumerable<Tuple<String, String, String, String, String, String, String>> cRoles =
                agr_agrs.Distinct(new LambdaComparer<Tuple<String, String, String, String, String, String, String>>((i, k) => i.Item1 == k.Item1));
            foreach (Tuple<String, String, String, String, String, String, String> agr in cRoles)
            {
                var singleRoles = (from agrs in agr_agrs
                                   where agrs.Item1 == agr.Item1
                                   select agrs.Item2).ToArray();
                OnLog(new LogEventArgs("Operazione in corso sulle seguenti coppie di ruoli"));
                foreach (String item in singleRoles)
                    OnLog(new LogEventArgs(String.Format("{0,30}\t{1,30}", agr.Item1, item)));
                DoOpSingleRoleCompositeRole(agr.Item1, singleRoles);
                OnLog(new LogEventArgs("OK"));
            }
        }

        private void DeleteSingleRole(List<Tuple<String, String, String, String, String, String, String>> t_agr_agrs)
        {
            try
            {
                foreach (Tuple<String, String, String, String, String, String, String> item in t_agr_agrs)
                {
                    RfcRepository repo = destination.Repository;
                    IRfcFunction func = repo.CreateFunction("PRGN_ACTIVITY_GROUP_DELETE");
                    func.SetValue("ACTIVITY_GROUP", item.Item1);
                    func.SetValue("ENQUEUE_AND_TRANSPORT", "");
                    func.SetValue("SHOW_DIALOG", "");
                    OnLog(new LogEventArgs(String.Format("Cancellazione del ruolo {0} in corso", item.Item1)));
                    func.Invoke(destination);
                    OnLog(new LogEventArgs("OK"));
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.WriteLine(ex.Message + " " + ex.StackTrace);
                OnError(new LogEventArgs(ex.Message));
            }
        }

        private void CreateDerivateRole(List<Tuple<String, String, String, String, String, String, String>> t_agr_agrs)
        {
            try
            {
                RfcRepository repo = destination.Repository;
                IRfcFunction func = repo.CreateFunction("PRGN_RFC_CREATE_ACTIVITY_GROUP"); ;
                IRfcTable tcodes = func.GetTable("TCODES");
                foreach (Tuple<String, String, String, String, String, String, String> item in t_agr_agrs)
                {
                    func = repo.CreateFunction("PRGN_RFC_CREATE_ACTIVITY_GROUP");
                    func.SetValue("ACTIVITY_GROUP", item.Item1);
                    func.SetValue("ACTIVITY_GROUP_TEXT", item.Item2);
                    func.SetValue("NO_DIALOG", "X");
                    func.SetValue("TEMPLATE", item.Item3);
                    OnLog(new LogEventArgs(String.Format("Creazione in corso del ruolo '{0,30}' dal template {1,80}", item.Item1, item.Item3)));
                    func.Invoke(destination);
                    OnLog(new LogEventArgs("OK"));
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.WriteLine(ex.Message + " " + ex.StackTrace);
                OnError(new LogEventArgs(ex.Message));
            }
        }

        private void CreateCompleteSingleRole(List<Tuple<String, String, String, String, String, String, String>> t_agr_agrs)
        {
            try
            {
                RfcRepository repo = destination.Repository;
                IRfcFunction func = repo.CreateFunction("PRGN_RFC_CREATE_ACTIVITY_GROUP"); ;
                IRfcTable tcodes = func.GetTable("TCODES");
                Tuple<String, String, String, String, String, String, String> oldTuple = null;
                foreach (Tuple<String, String, String, String, String, String, String> item in t_agr_agrs)
                {
                    if (oldTuple == null)
                        oldTuple = item;

                    if (oldTuple.Item1 != item.Item1)
                    {
                        func.SetValue("ACTIVITY_GROUP", oldTuple.Item1);
                        func.SetValue("ACTIVITY_GROUP_TEXT", oldTuple.Item2);
                        func.SetValue("NO_DIALOG", "X");
                        func.SetValue("TEMPLATE", "");
                        OnLog(new LogEventArgs(String.Format("Creazione in corso del ruolo {0,30}\t{1,80}", oldTuple.Item1, oldTuple.Item2)));
                        func.Invoke(destination);
                        OnLog(new LogEventArgs("OK"));
                        oldTuple = item;
                        func = repo.CreateFunction("PRGN_RFC_CREATE_ACTIVITY_GROUP");
                        tcodes = func.GetTable("TCODES");
                    }
                    tcodes.Append();
                    tcodes.SetValue("TCODE", item.Item3);
                }

                func.SetValue("ACTIVITY_GROUP", oldTuple.Item1);
                func.SetValue("ACTIVITY_GROUP_TEXT", oldTuple.Item2);
                func.SetValue("NO_DIALOG", "X");
                func.SetValue("TEMPLATE", "");
                OnLog(new LogEventArgs(String.Format("Creazione in corso del ruolo {0,30}\t{1,80}", oldTuple.Item1, oldTuple.Item2)));
                func.Invoke(destination);
                OnLog(new LogEventArgs("OK"));
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.WriteLine(ex.Message + " " + ex.StackTrace);
                OnError(new LogEventArgs(ex.Message));
            }
        }

        private void CreateNewRoles(List<Tuple<String, String, String, String, String, String, String>> t_agr_agrs)
        {
            try
            {
                foreach (Tuple<String, String, String, String, String, String, String> item in t_agr_agrs)
                {
                    RfcRepository repo = destination.Repository;
                    IRfcFunction func = repo.CreateFunction("PRGN_RFC_CREATE_AGR_MULTIPLE");
                    func.SetValue("ACTIVITY_GROUP", item.Item1);
                    func.SetValue("ACTIVITY_GROUP_TEXT", item.Item2);
                    func.SetValue("COMMENT_TEXT_LINE_1", " ");
                    String collettiveagr = String.Empty;
                    String roleType = String.Empty;
                    if (this.Operation == Operations.NewComposite)
                    {
                        collettiveagr = "X";
                        roleType = "collettivo";
                    }
                    if (this.Operation == Operations.NewSingle)
                    {
                        collettiveagr = "";
                        roleType = "singolo";
                    }
                    func.SetValue("COLLECTIVE_AGR", collettiveagr);
                    OnLog(new LogEventArgs(String.Format("Creazione in corso del ruolo {0}", roleType)));
                    //Console.WriteLine("{0}\t{1}", "".PadRight(30, '-'), "".PadRight(80, '-'));
                    OnLog(new LogEventArgs(String.Format("{0,30}\t{1,80}", item.Item1, item.Item2)));
                    func.Invoke(destination);
                    OnLog(new LogEventArgs("OK"));
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.WriteLine(ex.Message + " " + ex.StackTrace);
                OnError(new LogEventArgs(ex.Message));
            }
        }

        private void RenameRoles(List<Tuple<String, String, String, String, String, String, String>> t_agr_agrs)
        {
            try
            {
                foreach (Tuple<String, String, String, String, String, String, String> item in t_agr_agrs)
                {
                    RfcRepository repo = destination.Repository;
                    IRfcFunction func = repo.CreateFunction("PRGN_RFC_CHANGE_TEXTS");
                    func.SetValue("ACTIVITY_GROUP", item.Item1);
                    IRfcTable tSingleRoles = func.GetTable("TEXTS");
                    tSingleRoles.Append();
                    tSingleRoles.SetValue("AGR_NAME", item.Item1);
                    tSingleRoles.SetValue("LINE", 0);
                    tSingleRoles.SetValue("TEXT", item.Item2);
                    OnLog(new LogEventArgs(String.Format("Rinomina del ruolo \"{0}\" in corso", item.Item2)));
                    func.Invoke(destination);
                    OnLog(new LogEventArgs("OK"));
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.WriteLine(ex.Message + " " + ex.StackTrace);
                OnError(new LogEventArgs(ex.Message));
            }
        }

        private void DeleteTCodes(List<Tuple<String, String, String, String, String, String, String>> t_agr_agrs)
        {
            try
            {
                foreach (Tuple<String, String, String, String, String, String, String> item in t_agr_agrs)
                {
                    RfcRepository repo = destination.Repository;
                    IRfcFunction func = repo.CreateFunction("PRGN_RFC_DELETE_TRANSACTION");
                    func.SetValue("ACTIVITY_GROUP", item.Item1);
                    func.SetValue("TCODE", item.Item2);
                    //IRfcTable tSingleRoles = func.GetTable("TEXTS");
                    //tSingleRoles.Append();
                    //tSingleRoles.SetValue("AGR_NAME", item.Key);
                    //tSingleRoles.SetValue("LINE", 0);
                    //tSingleRoles.SetValue("TEXT", item.Value);
                    OnLog(new LogEventArgs(String.Format("Cancellazione transazione \"{1}\" dal ruolo \"{0}\" in corso", item.Item1, item.Item2)));
                    func.Invoke(destination);
                    OnLog(new LogEventArgs("OK"));
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.WriteLine(ex.Message + " " + ex.StackTrace);
                OnError(new LogEventArgs(ex.Message));
            }
        }

        private void AddTCodes(List<Tuple<String, String, String, String, String, String, String>> t_agr_agrs)
        {
            try
            {
                foreach (Tuple<String, String, String, String, String, String, String> item in t_agr_agrs)
                {
                    RfcRepository repo = destination.Repository;
                    IRfcFunction func = repo.CreateFunction("PRGN_RFC_ADD_TRANSACTION");
                    func.SetValue("ACTIVITY_GROUP", item.Item1);
                    func.SetValue("TCODE", item.Item2);
                    //IRfcTable tSingleRoles = func.GetTable("TEXTS");
                    //tSingleRoles.Append();
                    //tSingleRoles.SetValue("AGR_NAME", item.Key);
                    //tSingleRoles.SetValue("LINE", 0);
                    //tSingleRoles.SetValue("TEXT", item.Value);
                    OnLog(new LogEventArgs(String.Format("Attribuzione transazione \"{1}\" al ruolo \"{0}\" in corso", item.Item1, item.Item2)));
                    func.Invoke(destination);
                    OnLog(new LogEventArgs("OK"));
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.WriteLine(ex.Message + " " + ex.StackTrace);
                OnError(new LogEventArgs(ex.Message));
            }
        }

        public void CopyRole(List<Tuple<String, String, String, String, String, String, String>> t_agr_agrs)
        {
            try
            {
                foreach (Tuple<String, String, String, String, String, String, String> item in t_agr_agrs)
                {
                    RfcRepository repo = destination.Repository;
                    IRfcFunction func = repo.CreateFunction("/SDF/PRGN_COPY_AGR");
                    func.SetValue("SOURCE_AGR", item.Item1);
                    func.SetValue("TARGET_AGR", item.Item2);
                    OnLog(new LogEventArgs("Creazione del ruolo in corso: " + item.Item1));
                    func.Invoke(destination);
                    OnLog(new LogEventArgs("OK"));
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.WriteLine(ex.Message + " " + ex.StackTrace);
                OnError(new LogEventArgs(ex.Message));
            }
        }


        public void GenerateProfile(List<Tuple<String, String, String, String, String, String, String>> t_agr_agrs)
        {
            try
            {
                foreach (Tuple<String, String, String, String, String, String, String> item in t_agr_agrs)
                {
                    RfcRepository repo = destination.Repository;
                    IRfcFunction func = repo.CreateFunction("PRGN_PROFILE_NAME_GET");
                    func.SetValue("ACT_OBJID", item.Item1);
                    OnLog(new LogEventArgs("Creazione del profilo per " + item.Item1));
                    func.Invoke(destination);
                    String profName = func.GetString("ACT_PROFILE_NAME");
                    String profText = func.GetString("ACT_PROF_TEXT");
                    func = repo.CreateFunction("PRGN_AUTO_GENERATE_PROFILE_NEW");
                    func.SetValue("ACTIVITY_GROUP", item.Item1);
                    func.SetValue("PROFILE_NAME", profName);
                    func.SetValue("PROFILE_TEXT", profText);
                    func.SetValue("NO_DIALOG", "X");
                    func.SetValue("ORG_LEVELS_WITH_STAR", "X");
                    func.Invoke(destination);
                    OnLog(new LogEventArgs("OK"));
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.WriteLine(ex.Message + " " + ex.StackTrace);
                OnError(new LogEventArgs(ex.Message));
            }
        }

        public void UserAssignment(List<Tuple<String, String, String, String, String, String, String>> t_agr_agrs)
        {
            try
            {
                IEnumerable<Tuple<String, String, String, String, String, String, String>> cRoles =
                    t_agr_agrs.Distinct(new LambdaComparer<Tuple<String, String, String, String, String, String, String>>((i, k) => i.Item1 == k.Item1));
                foreach (Tuple<String, String, String, String, String, String, String> agr in cRoles)
                {
                    var singleRoles = t_agr_agrs.Where(a => a.Item1 == agr.Item1).ToList();
                    try
                    {
                        RfcRepository repo = destination.Repository;
                        String ops = "";
                        //if (this.Operation == Operations.RemoveRole)
                        //    ops = "BAPI_USER_ACTGROUPS_DELETE";
                        //if (this.Operation == Operations.AssignRole)
                        ops = "BAPI_USER_ACTGROUPS_ASSIGN";

                        OnLog(new LogEventArgs("Operazione su utente: " + agr.Item1));
                        IRfcFunction func = repo.CreateFunction(ops);
                        func.SetValue("USERNAME", agr.Item1);
                        IRfcTable roles = func.GetTable("ACTIVITYGROUPS");
                        foreach (var singleRole in singleRoles)
                        {
                            roles.Append();
                            if (this.Operation == Operations.AssignRole)
                            {
                                roles.SetValue("AGR_NAME", singleRole.Item2);
                                if (singleRole.Item3 != null)
                                    roles.SetValue("FROM_DAT", DateTime.ParseExact(singleRole.Item3, "dd.MM.yyyy", CultureInfo.InvariantCulture));
                                if (singleRole.Item4 != null)
                                    roles.SetValue("TO_DAT", DateTime.ParseExact(singleRole.Item4, "dd.MM.yyyy", CultureInfo.InvariantCulture));
                            }
                        }
                        func.Invoke(destination);
                    }
                    catch (Exception exc)
                    {
                        OnLog(new LogEventArgs(exc.Message));
                    }
                }
                OnLog(new LogEventArgs("Complete"));
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.WriteLine(ex.Message + " " + ex.StackTrace);
                OnError(new LogEventArgs(ex.Message));
            }
        }

        public void DoDownloadSpooljob(List<Tuple<String, String, String, String, String, String, String>> t_agr_agrs)
        {
            try
            {
                foreach (Tuple<String, String, String, String, String, String, String> item in t_agr_agrs)
                {
                    try
                    {
                        const int MAXROWS = 5000;
                        RfcRepository repo = destination.Repository;
                        int pages = 0;
                        do
                        {
                            IRfcFunction func = repo.CreateFunction("RSPO_RETURN_SPOOLJOB");
                            func.SetValue("RQIDENT", item.Item1);
                            func.SetValue("FNAME", item.Item2);
                            func.SetValue("LAST_LINE", MAXROWS);
                            func.SetValue("DESIRED_TYPE", "RAW");
                            OnLog(new LogEventArgs("Esportazione spool " + item.Item1 + " in corso"));
                            func.Invoke(destination);
                            using (TextWriter tw = File.CreateText(Path.Combine(item.Item3, item.Item2)))
                            {
                                IRfcTable buffer = func.GetTable("BUFFER");
                                for (int i = 0; i < buffer.Count; i++)
                                {
                                    buffer[i].GetString(0);
                                }
                                pages = buffer.Count;
                            }
                        } while (pages >= MAXROWS);
                    }
                    catch (Exception exc)
                    {
                        OnLog(new LogEventArgs(exc.Message));
                    }
                }
                OnLog(new LogEventArgs("Complete"));
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.WriteLine(ex.Message + " " + ex.StackTrace);
                OnError(new LogEventArgs(ex.Message));
            }
        }

        public void GetTabFieldInfo(List<Tuple<String, String, String, String, String, String, String>> t_agr_agrs)
        {
            try
            {
                Dictionary<string, string> fields = new Dictionary<string, string>() {
                    { "TABNAME", "CHAR"}, {"FIELDNAME", "CHAR"}, {"LANGU", "LANG"}, {"POSITION", "NUMC"}, {"OFFSET", "NUMC"}, {"DOMNAME", "CHAR"},
                    { "ROLLNAME", "CHAR"}, {"CHECKTABLE", "CHAR"}, {"LENG", "NUMC"}, {"INTLEN", "NUMC"}, {"OUTPUTLEN", "NUMC"}, {"DECIMALS", "NUMC"},
                    { "DATATYPE", "CHAR"}, {"INTTYPE", "CHAR"}, {"REFTABLE", "CHAR"}, {"REFFIELD", "CHAR"}, {"PRECFIELD", "CHAR"}, {"AUTHORID", "CHAR"},
                    { "MEMORYID", "CHAR"}, {"LOGFLAG", "CHAR"}, {"MASK", "CHAR"}, {"MASKLEN", "NUMC"}, {"CONVEXIT", "CHAR"}, {"HEADLEN", "NUMC"},
                    { "SCRLEN1", "NUMC"}, {"SCRLEN2", "NUMC"}, {"SCRLEN3", "NUMC"}, {"FIELDTEXT", "CHAR"}, {"REPTEXT", "CHAR"}, {"SCRTEXT_S", "CHAR"},
                    { "SCRTEXT_M", "CHAR"}, {"SCRTEXT_L", "CHAR"}, {"KEYFLAG", "CHAR"}, {"LOWERCASE", "CHAR"}, {"MAC", "CHAR"}, {"GENKEY", "CHAR"},
                    { "NOFORKEY", "CHAR"}, {"VALEXI", "CHAR"}, {"NOAUTHCH", "CHAR"}, {"SIGN", "CHAR"}, {"DYNPFLD", "CHAR"}, {"F4AVAILABL", "CHAR"},
                    { "COMPTYPE", "CHAR"}, {"LFIELDNAME", "CHAR"}, {"LTRFLDDIS", "CHAR"}, {"BIDICTRLC", "CHAR"}, {"OUTPUTSTYLE", "NUMC"},
                    { "NOHISTORY", "CHAR"}, {"AMPMFORMAT", "CHAR"} };

                // to get the location the assembly is executing from
                //(not necessarily where the it normally resides on disk)
                // in the case of the using shadow copies, for instance in NUnit tests, 
                // this will be in a temp directory.
                string path = System.Reflection.Assembly.GetExecutingAssembly().Location;

                //To get the location the assembly normally resides on disk or the install directory
                //string path = System.Reflection.Assembly.GetExecutingAssembly().CodeBase;

                //once you have the path you get the directory with:
                var directory = System.IO.Path.GetDirectoryName(path);

                using (TextWriter tw = File.CreateText(Path.Combine(directory, "EXPORTFIELD.txt")))
                {
                    tw.WriteLine(String.Join("\t", fields.Keys));
                    foreach (Tuple<String, String, String, String, String, String, String> item in t_agr_agrs)
                    {
                        try
                        {
                            RfcRepository repo = destination.Repository;
                            IRfcFunction func = repo.CreateFunction("DDIF_FIELDINFO_GET");
                            func.SetValue("TABNAME", item.Item1);
                            func.SetValue("UCLEN", "00");
                            OnLog(new LogEventArgs("Esportazione tabella " + item.Item1 + " in corso"));
                            func.Invoke(destination);
                            IRfcTable buffer = func.GetTable("DFIES_TAB");
                            for (int i = 0; i < buffer.Count; i++)
                            {
                                foreach (String field in fields.Keys)
                                {
                                    switch (fields[field])
                                    {
                                        case "LANG":
                                        case "CHAR": tw.Write(buffer[i].GetString(field) + "\t"); break;
                                        case "NUMC": tw.Write(buffer[i].GetDecimal(field) + "\t"); break;
                                    }
                                }
                                tw.WriteLine();
                            }
                            OnLog(new LogEventArgs("Complete"));
                        }
                        catch (Exception exc)
                        {
                            OnLog(new LogEventArgs(exc.Message));
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.WriteLine(ex.Message + " " + ex.StackTrace);
                OnError(new LogEventArgs(ex.Message));
            }
        }

        public void DoOnCompositeRoles(String agrFile)
        {
            String[] lines = File.ReadAllLines(agrFile);
            List<Tuple<String, String, String, String, String, String, String>> t_agr_agrs = new List<Tuple<String, String, String, String, String, String, String>>();
            foreach (String line in lines)
            {
                String[] split = line.Split('\t');
                var tupla = Tuple.Create<String, String, String, String, String, String, String>(
                    (split.Length > 0) ? split[0] : "",
                    (split.Length > 1) ? split[1] : "",
                    (split.Length > 2) ? split[2] : "",
                    (split.Length > 3) ? split[3] : "",
                    (split.Length > 4) ? split[4] : "",
                    (split.Length > 5) ? split[5] : "",
                    (split.Length > 6) ? split[6] : "");
                t_agr_agrs.Add(tupla);
            }
            raiseError = false;
            // Esegue l'operazione identificata
            Operations op = this.Operation.Value;
            doEvent[op](t_agr_agrs);
            if (!raiseError)
                OnComplete(new LogEventArgs("Completato"));
        }



        public static string ReadPassword()
        {
            Stack<String> passbits = new Stack<String>();
            //keep reading
            for (ConsoleKeyInfo cki = Console.ReadKey(true); cki.Key != ConsoleKey.Enter; cki = Console.ReadKey(true))
            {
                if (cki.Key == ConsoleKey.Backspace)
                {
                    //rollback the cursor and write a space so it looks backspaced to the user
                    Console.SetCursorPosition(Console.CursorLeft - 1, Console.CursorTop);
                    Console.Write(" ");
                    Console.SetCursorPosition(Console.CursorLeft - 1, Console.CursorTop);
                    if (passbits.Count > 0)
                        passbits.Pop();
                }
                else
                {
                    Console.Write("*");
                    passbits.Push(cki.KeyChar.ToString());
                }
            }
            string[] pass = passbits.ToArray();
            Array.Reverse(pass);
            return string.Join(string.Empty, pass);
        }

        public ProxyParameter ReadConnectionParam()
        {
            ProxyParameter param = new ProxyParameter();
            param.ProxyID = System.Guid.NewGuid().ToString();
            Console.Write("Application server: "); param.AppServerHost = Console.ReadLine();
            Console.Write("Client ID: "); param.Client = Console.ReadLine();
            Console.Write("Username: "); param.User = Console.ReadLine();
            Console.Write("Password: "); param.Password = ReadPassword();
            Console.WriteLine();
            Console.Write("System Number: "); param.SystemNumber = Console.ReadLine();
            Console.Write("Language [def. IT]: "); param.Language = Console.ReadLine();
            if (String.IsNullOrEmpty(param.Language))
                param.Language = "IT";

            /* param.AppServerHost = "svuni125";
            param.Client = "030";
            param.User = "kpmg";
            param.Password = "kpmg2012";
            param.SystemNumber = "10";
            param.Language = "IT"; */

            param.MaxPoolSize = "10";
            param.PoolSize = "1";
            param.IdleTimeout = "10";
            return param;
        }

        //private void EncryptConfigSection(string sectionKey)
        //{
        //    Configuration config = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None);
        //    ConfigurationSection section = config.GetSection(sectionKey);
        //    if (section != null)
        //    {
        //        if (!section.SectionInformation.IsProtected)
        //        {
        //            if (!section.ElementInformation.IsLocked)
        //            {
        //                section.SectionInformation.ProtectSection("DataProtectionConfigurationProvider");
        //                section.SectionInformation.ForceSave = true;
        //                config.Save(ConfigurationSaveMode.Full);
        //            }
        //        }
        //    }
        //}

        [STAThread]
        static void Main(string[] args)
        {
            System.Diagnostics.Trace.Listeners.Clear();
            System.Diagnostics.Trace.Listeners.Add(new System.Diagnostics.TextWriterTraceListener("oltrace.log"));
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new FormCompositeRoles());
        }
    }
}
