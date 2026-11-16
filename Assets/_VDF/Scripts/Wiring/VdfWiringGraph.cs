using System.Collections.Generic;

namespace VDF
{
    /// <summary>
    /// Una conexión entre dos bornes. Es simétrica: conectar A-B y B-A es lo mismo,
    /// así que los extremos se guardan siempre en orden canónico para poder comparar
    /// y evitar duplicados.
    /// </summary>
    public struct VdfConnection : System.IEquatable<VdfConnection>
    {
        public readonly string A;
        public readonly string B;

        public VdfConnection(string a, string b)
        {
            if (string.CompareOrdinal(a, b) <= 0) { A = a; B = b; }
            else { A = b; B = a; }
        }

        public bool Equals(VdfConnection otra)
        {
            return A == otra.A && B == otra.B;
        }

        public override bool Equals(object obj)
        {
            return obj is VdfConnection && Equals((VdfConnection)obj);
        }

        public override int GetHashCode()
        {
            int ha = A != null ? A.GetHashCode() : 0;
            int hb = B != null ? B.GetHashCode() : 0;
            return ha ^ (hb * 397);
        }

        public bool Toca(string id) { return A == id || B == id; }

        public string Otro(string id)
        {
            if (A == id) return B;
            if (B == id) return A;
            return null;
        }

        public override string ToString() { return A + " - " + B; }
    }

    /// <summary>
    /// Grafo de cableado: qué bornes están unidos por jumpers.
    ///
    /// C# puro y sin Unity, para poder probarlo entero en EditMode.
    ///
    /// La idea clave es que la corrección del cableado NO se juzga comparando cables
    /// uno a uno, sino comparando REDES ELÉCTRICAS. Unir A-B y B-C es eléctricamente
    /// idéntico a unir A-C y C-B: si se compararan pares, el simulador suspendería
    /// cableados que en el tablero real funcionan perfectamente.
    /// </summary>
    public sealed class VdfWiringGraph
    {
        private readonly List<VdfConnection> _conexiones = new List<VdfConnection>();

        public IReadOnlyList<VdfConnection> Conexiones { get { return _conexiones; } }
        public int Cuenta { get { return _conexiones.Count; } }

        /// <summary>Añade un cable. Devuelve false si es un bucle o si ya existía.</summary>
        public bool Conectar(string a, string b)
        {
            if (string.IsNullOrEmpty(a) || string.IsNullOrEmpty(b)) return false;
            if (a == b) return false;

            var c = new VdfConnection(a, b);
            if (_conexiones.Contains(c)) return false;

            _conexiones.Add(c);
            return true;
        }

        public bool Desconectar(string a, string b)
        {
            return _conexiones.Remove(new VdfConnection(a, b));
        }

        /// <summary>Quita todos los cables que llegan a un borne. Devuelve cuántos quitó.</summary>
        public int DesconectarTodoDe(string id)
        {
            return _conexiones.RemoveAll(c => c.Toca(id));
        }

        public bool Existe(string a, string b)
        {
            return _conexiones.Contains(new VdfConnection(a, b));
        }

        public void Limpiar() { _conexiones.Clear(); }

        /// <summary>Cables que llegan a un borne concreto.</summary>
        public List<VdfConnection> ConexionesDe(string id)
        {
            return _conexiones.FindAll(c => c.Toca(id));
        }

        /// <summary>
        /// Redes eléctricas: componentes conexas del grafo. Cada una es el conjunto de
        /// bornes que quedan al mismo potencial.
        /// </summary>
        public List<HashSet<string>> Redes()
        {
            var padre = new Dictionary<string, string>();

            System.Func<string, string> buscar = null;
            buscar = x =>
            {
                while (padre[x] != x) x = padre[x];
                return x;
            };

            for (int i = 0; i < _conexiones.Count; i++)
            {
                var c = _conexiones[i];
                if (!padre.ContainsKey(c.A)) padre[c.A] = c.A;
                if (!padre.ContainsKey(c.B)) padre[c.B] = c.B;
            }

            for (int i = 0; i < _conexiones.Count; i++)
            {
                var c = _conexiones[i];
                string ra = buscar(c.A);
                string rb = buscar(c.B);
                if (ra != rb) padre[ra] = rb;
            }

            var porRaiz = new Dictionary<string, HashSet<string>>();
            foreach (var kv in padre)
            {
                string raiz = buscar(kv.Key);
                HashSet<string> set;
                if (!porRaiz.TryGetValue(raiz, out set))
                {
                    set = new HashSet<string>();
                    porRaiz[raiz] = set;
                }
                set.Add(kv.Key);
            }

            return new List<HashSet<string>>(porRaiz.Values);
        }

        /// <summary>¿Estos dos bornes quedan al mismo potencial, aunque sea a través de otros?</summary>
        public bool MismaRed(string a, string b)
        {
            if (a == b) return true;

            var visitados = new HashSet<string>();
            var pendientes = new Queue<string>();
            pendientes.Enqueue(a);
            visitados.Add(a);

            while (pendientes.Count > 0)
            {
                string actual = pendientes.Dequeue();
                for (int i = 0; i < _conexiones.Count; i++)
                {
                    string vecino = _conexiones[i].Otro(actual);
                    if (vecino == null || visitados.Contains(vecino)) continue;
                    if (vecino == b) return true;
                    visitados.Add(vecino);
                    pendientes.Enqueue(vecino);
                }
            }

            return false;
        }
    }
}
