using RTSEngine.Entities;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace RTSEngine.UI
{
    public struct MultipleSelectionUIElement
    {
        public bool isSquad;
        public IEnumerable<IEntity> entities;
    }

    public struct MultipleSelectionTaskUIAttributes : ITaskUIAttributes
    {
        public MultipleSelectionTaskUIData data;

        public MultipleSelectionUIElement selectedElement;
    }
}
