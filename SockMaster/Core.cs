﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Xml;
using System.Net;
using mnn.design;
using mnn.net;

namespace SockMaster {
    class Core : CoreBaseNew {
        public static readonly string BASE_DIR = System.AppDomain.CurrentDomain.BaseDirectory;
        public static readonly string CONF_NAME = "SockMaster.xml";
        public static readonly string CONF_PATH = BASE_DIR + CONF_NAME;

        // hook for ui to display socket information
        public DataUI DataUI { get; set; }

        public Core()
        {
            DataUI = new DataUI();

            Config();

            // open core port
            IPEndPoint ep = SockSessServer.FindFreeEndPoint(IPAddress.Parse("0.0.0.0"), 5964);
            MakeListen(ep);
            DataUI.Port = ep.Port;
        }

        public void Config()
        {
            if (File.Exists(BASE_DIR + CONF_NAME) == false) {
                System.Windows.MessageBox.Show(CONF_NAME + ": can't find it.");
                return;
            }

            try {
                XmlDocument doc = new XmlDocument();
                doc.Load(BASE_DIR + CONF_NAME);

                /// ** config DataUI
                foreach (XmlNode item in doc.SelectNodes("/configuration/sockets/sockitem")) {
                    string[] str = item.Attributes["ep"].Value.Split(':');
                    IPEndPoint ep = new IPEndPoint(IPAddress.Parse(str[0]), int.Parse(str[1]));
                    SockType sockType = (SockType)Enum.Parse(typeof(SockType), item.Attributes["type"].Value);
                    SockUnit sockUnit = new SockUnit() {
                        ID = item.Attributes["id"].Value,
                        Name = item.Attributes["name"].Value,
                        Type = sockType,
                        Lep = sockType == SockType.listen ? ep : null,
                        Rep = sockType == SockType.connect ? ep : null,
                        State = SockState.Closed,
                        Autorun = bool.Parse(item.Attributes["autorun"].Value),
                    };
                    DataUI.AddSockUnit(sockUnit);

                    if (sockUnit.Autorun)
                        SockOpen(sockType, ep, sockUnit);
                }
            } catch (Exception) {
                System.Windows.MessageBox.Show(CONF_NAME + ": syntax error.");
            }
        }

        // SockSess Event

        protected override void AcceptEvent(object sender, SockSessAccept sess)
        {
            sess.close_event += new SockSessDelegate(CloseEvent);
            sess.recv_event += new SockSessDelegate(RecvEvent);
            sess_group.Add(sess);

            SockUnit sockUnit = new SockUnit() {
                ID = "at" + sess.rep.ToString(),
                Name = "accept",
                Type = SockType.accept,
                Lep = sess.lep,
                Rep = sess.rep,
                State = SockState.Opened,
            };
            DataUI.AddSockUnit(sockUnit);
        }

        protected override void CloseEvent(object sender)
        {
            SockSessNew sess = sender as SockSessNew;
            sess_group.Remove(sess);

            if (sender is SockSessServer) {
                SockUnit unit = DataUI.FindSockUnit(SockType.listen, sess.lep, sess.rep);
                DataUI.CloseSockUnit(unit);

            } else if (sender is SockSessClient) {
                SockUnit unit = DataUI.FindSockUnit(SockType.connect, sess.lep, sess.rep);
                DataUI.CloseSockUnit(unit);

            } else/* if (sender is SockSessAccept)*/ {
                SockUnit unit = DataUI.FindSockUnit(SockType.accept, sess.lep, sess.rep);
                DataUI.DelSockUnit(unit);
            }
        }

        protected override void RecvEvent(object sender)
        {
            SockSessNew sess = sender as SockSessNew;
            SockRequest request = new SockRequest(sess.lep, sess.rep, sess.rfifo.Take());
            SockResponse response = new SockResponse();

            dispatcher.Handle(request, ref response);
            if (response.data != null && response.data.Length != 0)
                sess.wfifo.Append(response.data);
        }

        // Center Service

        protected override void DefaultService(SockRequest request, ref SockResponse response)
        {
            string log = DateTime.Now + " (" + request.rep.ToString() + " => " + request.lep.ToString() + ")\n";
            log += SockConvert.ParseBytesToString(request.data) + "\n\n";

            /// ** update DataUI
            DataUI.Logger(log);
        }

        protected override void SockOpenService(SockRequest request, ref SockResponse response)
        {
            // get param string & parse to dictionary
            string msg = Encoding.UTF8.GetString(request.data);
            if (!msg.Contains('?')) return;
            msg = msg.Substring(msg.IndexOf('?') + 1);
            IDictionary<string, string> dc = SockConvert.ParseUrlQueryParam(msg);

            SockType sockType = (SockType)Enum.Parse(typeof(SockType), dc["type"]);
            IPEndPoint ep = new IPEndPoint(IPAddress.Parse(dc["ip"]), int.Parse(dc["port"]));
            SockUnit unit = DataUI.FindSockUnit(dc["id"]);
            if (unit == null)
                return;

            SockOpen(sockType, ep, unit);
        }

        private void SockOpen(SockType sockType, IPEndPoint ep, SockUnit unit)
        {
            try {
                SockSessNew sess = null;
                if (sockType == SockType.listen)
                    sess = MakeListen(ep);
                else
                    sess = MakeConnect(ep);
                DataUI.OpenSockUnit(unit, sess.lep, sess.rep);
            } catch (Exception) {
                DataUI.CloseSockUnit(unit);
            }
        }
    }
}
