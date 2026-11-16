// NOTA: clase estática, no es un componente. Ver E6 en VIABILIDAD.md.

using System.Collections.Generic;
using UnityEngine;

namespace VDF
{
    /// <summary>
    /// Crea cables con física entre bornes, anclados a los puntos de conexión reales.
    ///
    /// Es el reemplazo de la parábola calculada a mano de E4: misma entrada (el grafo de
    /// cableado) y misma salida visual (un cable entre dos bornes), pero ahora el cable
    /// cae por su peso y se apoya en lo que encuentra.
    /// </summary>
    public static class VdfCableSpawner
    {
        public const string NombreContenedor = "VDF_CablesFisica";

        private static readonly Color[] Paleta =
        {
            new Color(0.80f, 0.11f, 0.09f),   // rojo
            new Color(0.07f, 0.07f, 0.08f),   // negro
            new Color(0.10f, 0.30f, 0.75f),   // azul
            new Color(0.12f, 0.55f, 0.20f),   // verde
            new Color(0.90f, 0.78f, 0.08f),   // amarillo
            new Color(0.78f, 0.79f, 0.81f)    // gris
        };

        private static readonly Dictionary<string, Material> _materiales = new Dictionary<string, Material>();

        public static Material MaterialDe(Color color)
        {
            string clave = ColorUtility.ToHtmlStringRGB(color);

            Material mat;
            if (_materiales.TryGetValue(clave, out mat) && mat != null) return mat;

            // Lit, no Unlit: queremos que el cable reciba luz y tenga brillo de goma.
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Sprites/Default");

            mat = new Material(shader) { name = "VDF_CableFisica_" + clave };
            mat.SetColor("_BaseColor", color);
            mat.SetFloat("_Smoothness", 0.35f);
            mat.SetFloat("_Metallic", 0f);
            _materiales[clave] = mat;
            return mat;
        }

        /// <summary>Punto de anclaje estable sobre un borne, creado una sola vez.</summary>
        /// <summary>
        /// Altura a la que cuelgan los cables: la encimera del banco. Se pregunta al
        /// reconstructor para que siga cuadrando si el módulo cambia de sitio.
        /// </summary>
        private static float AlturaDeReposo
        {
            get { return VdfReconstructorEscena.AlturaEncimera; }
        }

        private static Transform AnclajeDe(VdfTerminal borne)
        {
            var existente = borne.transform.Find("AnclajeCable");
            if (existente != null) return existente;

            var go = new GameObject("AnclajeCable");
            go.transform.SetParent(borne.transform, false);
            go.transform.localPosition = borne.PuntoConexionLocal;
            return go.transform;
        }

        /// <summary>
        /// Tiende un cable entre dos bornes. La longitud se calcula con holgura sobre la
        /// distancia recta: sin holgura el cable queda tenso como una cuerda de guitarra,
        /// que es justo lo que NO parece un cable banana.
        /// </summary>
        public static VdfCablePhysics Tender(VdfTerminal a, VdfTerminal b, Color color,
                                             Transform contenedor, float holgura = 1.35f)
        {
            if (a == null || b == null) return null;

            var go = new GameObject("CableFisica_" + a.Id + "_" + b.Id);
            if (contenedor != null) go.transform.SetParent(contenedor, false);

            var cable = go.AddComponent<VdfCablePhysics>();
            // La longitud sale de dónde tiene que colgar el cable, no de lo lejos que estén
            // los bornes.
            //
            // En la foto del módulo real (Docs/Referencia/fotos) se ve lo que hacía falta
            // copiar: cada cable sale del borne, BAJA hasta cerca de la mesa y vuelve a subir
            // al otro. Dejándolo en manos de una holgura proporcional, dos bornes vecinos
            // quedaban unidos por una panza corta que se quedaba a media altura del tablero,
            // justo encima de los pulsadores de SUBIR y BAJAR y de los indicadores: se veían
            // cables atravesando botones, que es algo que en un tablero de verdad no pasa.
            //
            // Con la caída calculada, un cable mide su distancia más lo que baja y vuelve a
            // subir, igual que los de la foto, y la cara del tablero queda despejada salvo por
            // los dos tramos verticales que salen de cada borne.
            float recta = Vector3.Distance(a.PuntoConexion, b.PuntoConexion);
            float masBajo = Mathf.Min(a.PuntoConexion.y, b.PuntoConexion.y);
            float caida = Mathf.Max(0.10f, masBajo - (AlturaDeReposo + 0.04f));
            cable.Longitud = Mathf.Max(recta * holgura, recta + caida * 1.9f);
            // Un nodo cada 2 cm, no cada 4,5: con pocos nodos la cuerda describe polígonos
            // en vez de curvas y el cable se ve tieso, como un tubo doblado a martillazos.
            // Un cable banana de laboratorio es flexible y cae en curvas suaves.
            cable.Nodos = Mathf.Clamp(Mathf.RoundToInt(cable.Longitud / 0.020f) + 4, 12, 40);

            cable.Inicializar(AnclajeDe(a), AnclajeDe(b), color, MaterialDe(color));

            // Y se deja caer antes de entregarlo. Un cable recién tendido conserva su trazado
            // de nacimiento —una curva calculada, idéntica en todos— y se ve tieso; cayendo,
            // cuelga en catenaria y cada uno queda a su manera. Son cuatrocientos pasos de
            // solucionador sobre veinte nodos: ni se nota al cablear.
            cable.Asentar();
            return cable;
        }

        /// <summary>
        /// Tiende con física todo el cableado correcto de la práctica. Devuelve un resumen.
        /// </summary>
        public static string TenderCableadoCorrecto(float holgura = 1.35f)
        {
            var anterior = GameObject.Find(NombreContenedor);
            if (anterior != null)
            {
                if (Application.isPlaying) Object.Destroy(anterior);
                else Object.DestroyImmediate(anterior);
            }

            var contenedor = new GameObject(NombreContenedor);

            // Índice de bornes por Id.
            var bornes = new Dictionary<string, VdfTerminal>();
            var encontrados = Object.FindObjectsByType<VdfTerminal>();
            for (int i = 0; i < encontrados.Length; i++)
                if (!string.IsNullOrEmpty(encontrados[i].Id)) bornes[encontrados[i].Id] = encontrados[i];

            var redes = VdfWiringSpec.Practica();
            int tendidos = 0, fallidos = 0;

            for (int i = 0; i < redes.Count; i++)
            {
                var red = redes[i];
                Color color = Paleta[i % Paleta.Length];

                for (int j = 0; j + 1 < red.Bornes.Length; j++)
                {
                    VdfTerminal a, b;
                    if (!bornes.TryGetValue(red.Bornes[j], out a) ||
                        !bornes.TryGetValue(red.Bornes[j + 1], out b))
                    {
                        fallidos++;
                        continue;
                    }

                    if (Tender(a, b, color, contenedor.transform, holgura) != null) tendidos++;
                    else fallidos++;
                }
            }

            return "cables con fisica tendidos: " + tendidos + "   fallidos: " + fallidos
                 + "   colisionadores activos: " + VdfCablePhysics.Colisionadores.Count;
        }

        /// <summary>Estadísticas para comprobar que la simulación se comporta.</summary>
        public static string Diagnostico()
        {
            var cables = Object.FindObjectsByType<VdfCablePhysics>();
            if (cables.Length == 0) return "no hay cables con fisica";

            int nodos = 0;
            float minY = float.MaxValue, maxEstiramiento = 0f;

            for (int i = 0; i < cables.Length; i++)
            {
                nodos += cables[i].Nodos;
                minY = Mathf.Min(minY, cables[i].AlturaMinima());
                float ratio = cables[i].LongitudActual() / Mathf.Max(0.001f, cables[i].Longitud);
                maxEstiramiento = Mathf.Max(maxEstiramiento, ratio);
            }

            return "cables: " + cables.Length
                 + "   masas puntuales totales: " + nodos
                 + "   altura minima: " + minY.ToString("0.000") + " m"
                 + "   estiramiento maximo: " + maxEstiramiento.ToString("0.00") + "x";
        }
    }
}
