using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UEVR {
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

        [UserScopedSettingAttribute()]
        [DefaultSettingValueAttribute("true")]
        public bool NullifyVRPluginsCheckbox {
            get { return (bool)this["NullifyVRPluginsCheckbox"]; }
            set { this["NullifyVRPluginsCheckbox"] = value; }
        }

        [UserScopedSettingAttribute()]
        [DefaultSettingValueAttribute("false")]
        public bool IgnoreFutureVDWarnings {
            get { return (bool)this["IgnoreFutureVDWarnings"]; }
            set { this["IgnoreFutureVDWarnings"] = value; }
        }

        [UserScopedSettingAttribute()]
        [DefaultSettingValueAttribute("true")]
        public bool FocusGameOnInjection
        {
            get { return (bool)this["FocusGameOnInjection"]; }
            set { this["FocusGameOnInjection"] = value; }
        }
    }
}
