﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;

namespace mnn.service {
    public class Service : mnn.glue.IExecable {
        public delegate void ServiceHandlerDelegate(ServiceRequest request, ref ServiceResponse response);
        public delegate void ServiceBeforeDelegate(ref ServiceRequest request);
        public delegate void ServiceDoneDelegate(ServiceRequest request, ServiceResponse response);

        public string id { get; private set; }
        private ServiceHandlerDelegate func;
        private Queue<ServiceRequest> request_queue;
        public int request_count { get { return request_queue.Count; } }

        public ServiceBeforeDelegate service_before;
        public ServiceDoneDelegate service_done;

        public Service(string id, ServiceHandlerDelegate func, ServiceDoneDelegate done)
        {
            this.id = id;
            this.func = func;
            request_queue = new Queue<ServiceRequest>();
            service_before = null;
            service_done = done;
        }

        public bool Equals(Service serv)
        {
            if (id.Equals(serv.id))
                return true;
            else
                return false;
        }

        public virtual bool IsMatch(ServiceRequest request)
        {
            if (id.Equals(request.id))
                return true;
            else
                return false;
        }

        public void AddRequest(ServiceRequest request)
        {
            if (request_queue.Count > 2048) {
                log4net.LogManager.GetLogger(typeof(Service))
                    .Fatal("pack_queue's count is larger than 2048!");
                request_queue.Clear();
            } else {
                request_queue.Enqueue(request);
            }
        }

        public void DoService()
        {
            while (request_queue.Count != 0) {
                var request = request_queue.Dequeue();

                ServiceResponse response = new ServiceResponse() {
                    id = request.id,
                    errcode = 0,
                    errmsg = "",
                    data = "",
                };

                DoServiceDirect(request, ref response);
            }
        }

        public void DoServiceDirect(ServiceRequest request, ref ServiceResponse response)
        {
            try {
                if (service_before != null)
                    service_before(ref request);
                func(request, ref response);
                if (service_done != null)
                    service_done(request, response);
            } catch (Exception ex) {
                log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType)
                    .Warn("error in handling service " + request.id, ex);
            }
        }

        public void DoExec()
        {
            DoService();
        }

        public bool IsClosed()
        {
            return false;
        }
    }
}
