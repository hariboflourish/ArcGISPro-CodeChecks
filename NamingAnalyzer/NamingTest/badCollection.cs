using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace badnamespace
{
    // ❌ Class name not PascalCase
    public class layerService
    {
        // ❌ Field missing _ prefix
        // ❌ Collection without valid suffix
        private List<string> _layer;

        // ❌ Field naming wrong + collection suffix wrong
        private HashSet<int> _selected;

        // ❌ Const not ALL_CAPS
        public const int maxLayerCount = 5;

        // ❌ Property not PascalCase
        public IEnumerable<int> visibleitems { get; set; }

        // ❌ Method name not PascalCase
        public void load_layers()
        {
            // ❌ Local collection naming (not yet enforced, but good example)
            int[] value = new int[5];

            Console.WriteLine(value.Length);
        }
    }

    // ❌ Command class naming wrong
    internal class Exportmapcommand : Button
    {
    }

    // ❌ DockPane naming wrong
    internal class Elevationanalysisdockpane : DockPane
    {
    }

    // ❌ Collection field with misleading name
    public class CollectionTest
    {
        private ObservableCollection<string> _data;
    }
}

