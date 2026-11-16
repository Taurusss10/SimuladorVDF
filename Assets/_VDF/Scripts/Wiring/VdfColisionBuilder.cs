
using UnityEngine;

namespace VDF
{
    public static class VdfColisionBuilder
    {
        public const string NombreRaiz = "VDF_ColisionCables";

        /// <summary>
        /// Reconstruye todas las formas de colisión de la escena actual.
        /// Devuelve un resumen de lo montado.
        /// </summary>
        public static string Montar()
        {
            var anterior = GameObject.Find(NombreRaiz);
            if (anterior != null) Destruir(anterior);

            var raiz = new GameObject(NombreRaiz);
            var sb = new System.Text.StringBuilder();
            int n = 0;

            // --- La encimera: el cable se apoya en ella -----------------------------------
            // Es la forma más importante de todas: sin ella los cables caen al infinito.
            var encimera = BuscarPorNombre("Encimera");
            if (encimera != null)
            {
                Bounds b;
                if (Limites(encimera, out b))
                {
                    PlanoHorizontal(raiz.transform, "Plano_Mesa", b.max.y);
                    sb.AppendLine("  Plano_Mesa en y=" + b.max.y.ToString("0.000"));
                    n++;
                }
            }
            else
            {
                // Sin banco construido, se deja el valor por defecto para no quedarse sin suelo.
                PlanoHorizontal(raiz.transform, "Plano_Mesa", 0.75f);
                sb.AppendLine("  Plano_Mesa en y=0.750 (no se encontró la encimera)");
                n++;
            }

            // --- La placa del tablero: el cable no la atraviesa ---------------------------
            var placa = BuscarEnTablero("Placa");
            if (placa != null)
            {
                Bounds b;
                if (Limites(placa, out b))
                {
                    // La cara útil es la que mira al usuario, en -Z.
                    PlanoFrontal(raiz.transform, "Plano_Tablero", b.min.z);
                    sb.AppendLine("  Plano_Tablero en z=" + b.min.z.ToString("0.000"));
                    n++;
                }
            }

            // --- Cuerpos que sobresalen del tablero ---------------------------------------
            n += CajaDesde(raiz.transform, "Caja_V1000", BuscarEnTablero("Variador_V1000"), sb);
            n += CajaDesde(raiz.transform, "Caja_Breaker", BuscarEnTablero("Bloque_BREAKER"), sb);
            n += CajaDesde(raiz.transform, "Caja_Regleta", BuscarEnTablero("Bloque_PLACA_REGLETA"), sb);

            // --- El motor, que ahora sí va donde está de verdad ---------------------------
            var motor = GameObject.Find(VdfPanelBuilder.NombreMotor);
            if (motor != null)
            {
                Bounds b;
                if (Limites(motor.transform, out b))
                {
                    var go = Nodo(raiz.transform, "Cilindro_Motor", b.center);
                    var c = go.AddComponent<VdfColisionadorCable>();
                    c.Tipo = VdfColisionadorCable.Forma.Cilindro;
                    // El eje del motor va a lo largo de X, que es como lo construye el builder.
                    c.MediaLongitud = b.extents.x;
                    c.Radio = Mathf.Max(b.extents.y, b.extents.z);
                    sb.AppendLine("  Cilindro_Motor en " + Redondear(b.center) +
                                  " radio=" + c.Radio.ToString("0.000"));
                    n++;
                }
            }

            // --- El PLC, que también está sobre la mesa -----------------------------------
            var plc = GameObject.Find("VDF_PLC");
            if (plc != null) n += CajaDesde(raiz.transform, "Caja_PLC", plc.transform, sb);

            return "formas de colision montadas: " + n + "\n" + sb.ToString();
        }

        // =====================================================================================

        private static int CajaDesde(Transform padre, string nombre, Transform objetivo,
                                     System.Text.StringBuilder sb)
        {
            if (objetivo == null) return 0;

            Bounds b;
            if (!Limites(objetivo, out b)) return 0;

            var go = Nodo(padre, nombre, b.center);
            var c = go.AddComponent<VdfColisionadorCable>();
            c.Tipo = VdfColisionadorCable.Forma.Caja;
            c.Medidas = b.extents;

            sb.AppendLine("  " + nombre + " en " + Redondear(b.center) +
                          " medidas=" + Redondear(b.extents));
            return 1;
        }

        private static void PlanoHorizontal(Transform padre, string nombre, float y)
        {
            var go = Nodo(padre, nombre, new Vector3(0f, y, 0f));
            var c = go.AddComponent<VdfColisionadorCable>();
            c.Tipo = VdfColisionadorCable.Forma.Plano;
            c.NormalPlano = Vector3.up;
        }

        private static void PlanoFrontal(Transform padre, string nombre, float z)
        {
            var go = Nodo(padre, nombre, new Vector3(0f, 0f, z));
            var c = go.AddComponent<VdfColisionadorCable>();
            c.Tipo = VdfColisionadorCable.Forma.Plano;
            // Hacia el usuario: el cable se queda por delante de la placa.
            c.NormalPlano = Vector3.back;
        }

        private static GameObject Nodo(Transform padre, string nombre, Vector3 posicion)
        {
            var go = new GameObject(nombre);
            go.transform.SetParent(padre, false);
            go.transform.position = posicion;
            return go;
        }

        /// <summary>
        /// Límites en mundo de todos los renderers de un objeto. false si no tiene ninguno.
        ///
        /// Público para poder probarlo: es donde estuvo el fallo que dejó el plano de la
        /// mesa dos centímetros por debajo de la encimera.
        /// </summary>
        public static bool Limites(Transform t, out Bounds b)
        {
            b = new Bounds();
            var rs = t.GetComponentsInChildren<Renderer>();
            if (rs.Length == 0) return false;

            // Se parte de los límites COMPLETOS del primero, no de un punto en su centro.
            // Con `new Bounds(rs[0].bounds.center, Vector3.zero)` se perdían las dimensiones
            // del primer renderer: un objeto con un solo renderer salía con tamaño cero, y
            // la encimera daba su centro (0,73) en vez de su cara superior (0,75), dejando
            // el plano donde se apoyan los cables dos centímetros por debajo de la mesa.
            b = rs[0].bounds;
            for (int i = 1; i < rs.Length; i++) b.Encapsulate(rs[i].bounds);
            return true;
        }

        private static Transform BuscarEnTablero(string nombre)
        {
            var tablero = GameObject.Find(VdfPanelBuilder.NombreRaiz);
            return tablero != null ? BuscarPorNombre(nombre, tablero.transform) : null;
        }

        private static Transform BuscarPorNombre(string nombre, Transform raiz = null)
        {
            if (raiz != null)
            {
                var todos = raiz.GetComponentsInChildren<Transform>(true);
                for (int i = 0; i < todos.Length; i++)
                    if (todos[i].name == nombre) return todos[i];
                return null;
            }

            var go = GameObject.Find(nombre);
            if (go != null) return go.transform;

            foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include))
                if (t.name == nombre) return t;
            return null;
        }

        private static string Redondear(Vector3 v)
        {
            return "(" + v.x.ToString("0.00") + ", " + v.y.ToString("0.00") + ", " + v.z.ToString("0.00") + ")";
        }

        private static void Destruir(GameObject go)
        {
            if (Application.isPlaying) Object.Destroy(go);
            else Object.DestroyImmediate(go);
        }
    }
}
