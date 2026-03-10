using System;
using System.Collections.Generic;

namespace badnamespace
{
	var gg = "Hello";
    // 1️⃣ Class name not PascalCase
    public class layer_manager
    {
        // 4️⃣ Global field without _ prefix
        private string _mappath;

        // 9️⃣ Global const not ALL_CAPS
        public const int maxLayerCount = 10;

        // 3️⃣ Local variable not camelCase
        public void load_map_layers()
        {
            int SelectedIndex = 0;

            // 8️⃣ Collection without proper suffix
            List<string> layer = new List<string>();

            Console.WriteLine(SelectedIndex);
        }

        // 2️⃣ Method name not PascalCase
        public bool export_to_shapefile()
        {
            return true;
        }

        // 5️⃣ Property not PascalCase
        public bool isbusy { get; set; }
    }

    // 6️⃣ Command / Tool class naming wrong
    internal class Exportmap : Button
    {
    }

    // 6️⃣ DockPane naming wrong
    internal class Elevationanalysis : DockPane
    {
    }

    // 3️⃣ Local const should be PascalCase
    public class ConstTest
    {
        public void Test()
        {
            const int default_zoom_level = 5;
        }
    }
}

