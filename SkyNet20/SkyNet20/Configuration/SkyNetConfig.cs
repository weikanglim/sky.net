using System.Configuration;

namespace SkyNet20.Configuration
{
    public class SkyNetConfig : ConfigurationSection
    {
        [ConfigurationProperty("", IsRequired = true, IsDefaultCollection = true)]
        public SkyNetMachines Instances
        {
            get { return (SkyNetMachines)this[""];  }
            set { this[""] = value; }
        }
    }
}
