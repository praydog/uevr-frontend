using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UnrealVR {
    sealed class MainWindowSettings : ApplicationSettingsBase {
        [UserScopedSettingAttribute()]
        [DefaultSettingValueAttribute("false")]
        public bool OpenXRRadio {
            get { return (bool)this["OpenXRRadio"]; }
            set { this["OpenXRRadio"] = value; }
        }

        [UserScopedSettingAttribute()]
        [DefaultSettingValueAttribute("true")]
        public bool OpenVRRadio {
            get { return (bool)this["OpenVRRadio"]; }
            set { this["OpenVRRadio"] = value; }
        }
    }
}
