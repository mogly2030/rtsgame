using System.Collections.Generic;
using System.Linq;

using UnityEngine;

using RTSEngine.Faction;
using RTSEngine.Utilities;

namespace RTSEngine.Entities
{
    [System.Serializable]
    public class FactionTypeFilteredFactionEntities : TypeFilteredValue<FactionTypeInfo, IEnumerable<IFactionEntity>>
    {
        [SerializeField, EnforceType(typeof(IFactionEntity))]
        protected new GameObject[] allTypes = new GameObject[0];

        [System.Serializable]
        public struct Element
        {
            public FactionTypeInfo[] factionTypes;
            [EnforceType(typeof(IFactionEntity))]
            public GameObject[] factionEntities;
        }
        [SerializeField]
        private Element[] typeSpecific = new Element[0];

        public IEnumerable<IFactionEntity> GetAll()
        {
            IEnumerable<IFactionEntity> all = allTypes.FromGameObject<IFactionEntity>();

            foreach (Element element in typeSpecific)
                all = all
                    .Concat(element.factionEntities.FromGameObject<IFactionEntity>());

            return all;
        }

        public override IEnumerable<IFactionEntity> GetFiltered(FactionTypeInfo factionType)
        {
            List<IFactionEntity> filtered = new List<IFactionEntity>();
            filtered.AddRange(allTypes.FromGameObject<IFactionEntity>());

            foreach (Element element in typeSpecific)
            {
                if (factionType.IsValid() && element.factionTypes.Contains(factionType))
                    filtered.AddRange(element.factionEntities.FromGameObject<IFactionEntity>());
            }

            return filtered;
        }

        public IReadOnlyList<IFactionEntity> GetFiltered(FactionTypeInfo factionType, out IReadOnlyList<IFactionEntity> rest)
        {
            List<IFactionEntity> filtered = new List<IFactionEntity>();
            filtered.AddRange(allTypes.FromGameObject<IFactionEntity>());

            List<IFactionEntity> restList = new List<IFactionEntity>();

            foreach (Element element in typeSpecific)
            {
                if (factionType.IsValid() && element.factionTypes.Contains(factionType))
                    filtered.AddRange(element.factionEntities.FromGameObject<IFactionEntity>());
                else
                    restList.AddRange(element.factionEntities.FromGameObject<IFactionEntity>());
            }

            rest = restList;
            return filtered;
        }

    }
}
