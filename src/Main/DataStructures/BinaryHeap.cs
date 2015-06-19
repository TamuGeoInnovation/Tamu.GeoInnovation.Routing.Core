using System.Collections.Generic;

namespace USC.GISResearchLab.Routing.DataStructures
{
    public sealed class BinaryHeap<T, C>
        where C : IComparer<T>, new()
    {
        List<T> vertices; //heap only contains the vertices of the graph
        Dictionary<T, int> Vertex2pos; //track the position of a Vertex in the heap
        public int Count { get { return Vertex2pos.Count; } }
        int tail;
        C comp;

        public bool IsEmpty
        {
            get { return tail < 0; }
        }

        public BinaryHeap(int capacity, C comparer)
        {
            vertices = new List<T>(capacity);
            Vertex2pos = new Dictionary<T, int>(capacity);
            tail = -1;
            comp = comparer;
        }

        public bool Contains(T n)
        {
            return this.Vertex2pos.ContainsKey(n);
        }

        /**
         * build a heap out of the graph's Vertexs
         * expected performance: O(n) : n= number of Vertexs of the graph
         * complexity: O(n)
         */
        public void BuildHeap()
        {
            int cnt;
            cnt = tail >> 1;
            for (int i = cnt; i >= 0; i--)
            {
                Heapify(i);
            }
        }

        /**
         * this function maintains the heap property for a particular Vertex
         * int i: the serial number of a Vertex in heap
         * complexity: O(lg n)
         */
        public void Heapify(int i)
        {
            int l, r, min; //(l)eft child, (r)ight child, (min)imum key among l, r and i; 
            T tmp;
            l = (i << 1) + 1; //use shift << instead of multiply *
            r = (i << 1) + 2;
            if (l <= tail && (comp.Compare(vertices[l], vertices[i]) < 0))
                min = l;
            else
                min = i;
            if (r <= tail && (comp.Compare(vertices[r], vertices[min]) < 0))
                min = r;
            if (min != i)
            {
                //exchange Vertex at i with Vertex with minimum key value, either left or right child
                tmp = vertices[i];
                vertices[i] = vertices[min];
                vertices[min] = tmp;
                //modify the position
                Vertex2pos[vertices[min]] = min;
                Vertex2pos[vertices[i]] = i;
                Heapify(min);
            }
        }

        public void DecreaseKey(T vertex)
        {
            int p, curr;
            T tmp;
            try
            {
                // vertex.g = gval;
                curr = (int)Vertex2pos[vertex]; //!!!EXP
                p = (curr - 1) >> 1;
                while (p >= 0 && (comp.Compare(vertices[curr], vertices[p]) < 0))   //'float it up'   !!!EXP
                {
                    tmp = vertices[p];
                    vertices[p] = vertices[curr];
                    vertices[curr] = tmp;

                    //modify the position, !!!EXP
                    Vertex2pos[vertices[p]] = p;
                    Vertex2pos[vertices[curr]] = curr;
                    curr = p;
                    p = (curr - 1) >> 1;
                }
            }
            catch
            {
                p = 1;
            }
        }

        /**
          * extract the Vertex that has smallest key value---goal distance
          * expected performance: O(lg n) : n= number of Vertexs of the graph
          */
        public T ExtractMin()
        {
            T min = default(T);
            if (tail >= 0)
            {
                min = vertices[0];
                vertices[0] = vertices[tail];
                Vertex2pos[vertices[tail--]] = 0;
                Vertex2pos.Remove(min);
                vertices.RemoveAt(tail + 1);
                Heapify(0);
            }
            return min;
        }

        public void Add(T n)
        {
            tail++;
            vertices.Add(n);
            Vertex2pos.Add(n, tail);
            DecreaseKey(n);
        }

        public void Clear()
        {
            this.Vertex2pos.Clear();
            this.vertices.Clear();
            tail = -1;
        }
    }
}