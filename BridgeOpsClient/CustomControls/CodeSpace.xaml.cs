using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using ICSharpCode.AvalonEdit;
using ICSharpCode.AvalonEdit.Highlighting.Xshd;
using ICSharpCode.AvalonEdit.Highlighting;
using static System.Windows.Forms.VisualStyles.VisualStyleElement.TextBox;
using System.Reflection;
using System.Xml;

namespace BridgeOpsClient.CustomControls
{
    public partial class CodeSpace : TextEditor
    {
        public CodeSpace()
        {
            InitializeComponent();

            // Get the current assembly
            var assembly = Assembly.GetExecutingAssembly();

            // Define the resource name
            string resourceName = "BridgeOpsClient.Resources.sql-syntax.xshd";

            // Load the embedded resource
            using (Stream stream = assembly.GetManifestResourceStream(resourceName)!)
            {
                if (stream == null)
                    throw new InvalidOperationException("Could not find embedded resource: " + resourceName);

                using (XmlTextReader reader = new XmlTextReader(stream))
                {
                    // Load the syntax highlighting definition
                    var highlighting = HighlightingLoader.Load(reader, HighlightingManager.Instance);
                    // Apply the syntax highlighting to the TextEditor
                    SyntaxHighlighting = highlighting;
                }
            }

            
        }
    }
}
