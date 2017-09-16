using System;
using System.Collections.Generic;
using System.Text;
using System.Configuration;

namespace SkyNet20.Configuration
{
    public class SkyNetMachines : ConfigurationElementCollection
    {
        public SkyNetMachine this[int index]
        {
            get
            {
                return base.BaseGet(index) as SkyNetMachine;
            }
            set
            {
                if (base.BaseGet(index) != null)
                {
                    base.BaseRemoveAt(index);
                }

                base.BaseAdd(index, value);
            }
        }

        public new SkyNetMachine this[string hostname]
        {
            get
            {
                return (SkyNetMachine) base.BaseGet(hostname);
            }
            set
            {
                if (base.BaseGet(hostname) != null)
                {
                    base.BaseRemoveAt(BaseIndexOf(BaseGet(hostname)));
                }

                base.BaseAdd(value);
            }
        }

        protected override ConfigurationElement CreateNewElement()
        {
            return new SkyNetMachine();
        }

        protected override object GetElementKey(ConfigurationElement element)
        {
            return ((SkyNetMachine)element).HostName;
        }
    }
}
