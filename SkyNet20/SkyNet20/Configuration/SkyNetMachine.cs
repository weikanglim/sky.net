using System;
using System.Collections.Generic;
using System.Text;
using System.Configuration;

namespace SkyNet20.Configuration
{
    public class SkyNetMachine : ConfigurationElement
    {
        [ConfigurationProperty("hostname", IsRequired = true, IsKey = true)]
        public String HostName
        {
            get
            {
                return this["hostname"] as string;
            }
        }

        [ConfigurationProperty("IsIntroducer", IsRequired = false, IsKey = false)]
        public bool IsIntroducer
        {
            get
            {
                bool? introducer = this["IsIntroducer"] as bool?;
                return introducer ?? false;
            }
        }
    }
}
