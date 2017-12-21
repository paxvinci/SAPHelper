using SAP.Middleware.Connector;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace ManageCompositeRole
{
    class SapRsparam
    {
        private ManageCompositeRole connect;
        private IRfcFunction function;
        private MemoryStream stream;

        public SapRsparam(ProxyParameter param)
        {
            this.connect = new ManageCompositeRole();
            this.connect.Login(param);            
        }

        public void Execute()
        {
            //function = connect.Destination.Repository.CreateFunction("PFL_MODIFY_PARAMETER");
            //function.
        }
    }
}
