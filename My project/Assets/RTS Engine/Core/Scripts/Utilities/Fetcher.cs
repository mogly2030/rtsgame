using UnityEngine;

namespace RTSEngine.Utilities
{
    /// <summary>
    /// Defines the types of fetching an element from a set where:
    /// random: One item is randomly chosen each time.
    /// randomNoRep: One item is randomly chosen each time with the guarantee that the same item will not be chosen consecutively.
    /// inOrder: Fetch items in the order they were defined in.
    /// </summary>
    public enum FetchType { random, randomNoRep, inOrder }

    [System.Serializable]
    public abstract class Fetcher<T> where T : Object
    {
        #region Class Attributes
        [SerializeField, Tooltip("How would the item be fetched each time?")]
        private FetchType fetchType = FetchType.random;

        [SerializeField, Tooltip("An array of items that can be potentially fetched.")]
        private T[] items = new T[0];

        public int Count
        {
            get
            {
                return items.Length;
            }
        }

        private int cursor = 0; //for the inOrder and randomNoRep fetch types, this is used to track the last fetched items.
        #endregion

        #region Fetching
        private T GetNext ()
        {
            //move the cursor one step further through the array
            if (cursor >= items.Length - 1)
                cursor = 0;
            else
                cursor++;

            return items[cursor]; //return the next item in the array
        }
        private T GetPrevious ()
        {
            if (cursor == 0)
                cursor = items.Length - 1;
            else
                cursor--;

            return items[cursor]; 
        }

        public virtual T Fetch ()
        {
            if (!CanFetch() 
                || items.Length <= 0)
                return null;

            OnPreFetch();

            switch (fetchType)
            {
                case FetchType.randomNoRep:

                    int itemIndex = Random.Range(0, items.Length); 

                    if (itemIndex == cursor) 
                        return GetNext(); 
                    else 
                    {
                        cursor = itemIndex; 
                        return items[cursor];
                    }

                case FetchType.inOrder: 
                    return GetNext();

                default:
                    cursor = Random.Range(0, items.Length);
                    return items[cursor];
            }
        }

        public virtual T FetchNext()
        {
            if (!CanFetch() 
                || items.Length <= 0)
                return null;

            OnPreFetch();

            return GetNext();
        }

        public virtual T FetchPrevious()
        {
            if (!CanFetch() 
                || items.Length <= 0)
                return null;

            OnPreFetch();

            return GetPrevious();
        }

        public virtual T Fetch(int index)
        {
            if (!CanFetch() 
                || items.Length <= 0
                || index.IsValidIndex(items))
                return null;

            OnPreFetch();

            cursor = index;
            return items[index];
        }

        protected virtual void OnPreFetch() { }

        protected virtual bool CanFetch() => true;
        #endregion
    }
}
