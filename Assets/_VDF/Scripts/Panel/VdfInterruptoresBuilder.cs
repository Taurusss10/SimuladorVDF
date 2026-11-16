using TMPro;
using UnityEngine;

namespace VDF
{
    public static class VdfInterruptoresBuilder
    {
        public const float Ancho = 0.20f;
        public const float Alto = 0.110f;
        public const float Fondo = 0.05f;

        /// <summary>Separación entre interruptores.</summary>
        public const float Paso = 0.062f;

        /// <summary>Entre la bandeja de cables y el PLC, delante, para que quede a mano.</summary>
        public static readonly Vector3 Posicion =
            new Vector3(0.15f, VdfReconstructorEscena.AlturaEncimera, -0.50f);

        /// <summary>Un 25 % más grande que lo dibujado: a 0,58 m quedaba al borde de la mesa y los rótulos no se leían.</summary>
        public const float Escala = 1.25f;

        /// <summary>Girada hacia el alumno e inclinada hacia atrás, como el PLC.</summary>
        public static readonly Vector3 Giro = new Vector3(18f, -8f, 0f);

        /// <summary>Nombres de los tres nodos. Lo que va tras el último «_» es lo que enseña la etiqueta del visor.</summary>
        public const string NombreMarcha = "Interruptor_S4";
        public const string NombreS5 = "Interruptor_S5";
        public const string NombreS6 = "Interruptor_S6";

        public static GameObject Construir()
        {
            var raiz = new GameObject(VdfSimulacion.NombreInterruptores);
            raiz.transform.position = Posicion;
            raiz.transform.rotation = Quaternion.Euler(Giro);
            raiz.transform.localScale = Vector3.one * Escala;

            Color gris = new Color(0.20f, 0.21f, 0.23f);
            Color negro = new Color(0.08f, 0.08f, 0.09f);
            Color metal = new Color(0.78f, 0.79f, 0.80f);
            Color blanco = new Color(0.95f, 0.95f, 0.93f);

            var caja = Cubo(raiz.transform, "Caja", new Vector3(0f, Alto * 0.5f, 0f),
                            new Vector3(Ancho, Alto, Fondo), gris, 0.25f);
            Quitar(caja.GetComponent<Collider>());

            float frente = -Fondo * 0.5f;
            Texto(raiz.transform, "Titulo", "ENTRADAS DEL PLC",
                  new Vector3(0f, Alto - 0.010f, frente - 0.001f), new Vector2(Ancho * 0.8f, 0.012f), blanco);

            // Rótulo con la entrada del PLC a la que va cada interruptor, como en el Ladder
            // del laboratorio: I0.0 la marcha, I0.1 y I0.2 S5 y S6.
            Interruptor(raiz.transform, NombreMarcha, "S4\nI0.0", VdfAccionPulsador.InterruptorMarcha,
                        -Paso, frente, negro, metal, blanco);
            Interruptor(raiz.transform, NombreS5, "S5\nI0.1", VdfAccionPulsador.InterruptorS5,
                        0f, frente, negro, metal, blanco);
            Interruptor(raiz.transform, NombreS6, "S6\nI0.2", VdfAccionPulsador.InterruptorS6,
                        Paso, frente, negro, metal, blanco);

#if UNITY_EDITOR
            // ApoyarSobreEncimera vive en el bloque de editor del reconstructor; la caja solo
            // se construye al reconstruir la escena, así que en el build no hace falta.
            VdfReconstructorEscena.ApoyarSobreEncimera(raiz, 0f);
#endif
            return raiz;
        }

        private static void Interruptor(Transform padre, string nombre, string rotulo,
                                        VdfAccionPulsador accion, float x, float frente,
                                        Color negro, Color metal, Color blanco)
        {
            // Arriba en la caja, para que el rótulo de debajo (S4 I0.0...) quede sobre su
            // cara oscura. Colgando por fuera era blanco sobre blanco y no se leía.
            float y = Alto - 0.048f;

            var nodo = new GameObject(nombre);
            nodo.transform.SetParent(padre, false);
            nodo.transform.localPosition = new Vector3(x, y, frente);

            // Placa del interruptor, pegada al frente de la caja.
            var placa = Cubo(nodo.transform, "Placa", new Vector3(0f, 0f, -0.0015f),
                             new Vector3(0.030f, 0.040f, 0.003f), negro, 0.3f);
            Quitar(placa.GetComponent<Collider>());

            // La palanca gira alrededor de este pivote (ver VdfPulsadorTablero.RefrescarPalanca).
            var pivote = new GameObject("Palanca");
            pivote.transform.SetParent(nodo.transform, false);
            pivote.transform.localPosition = new Vector3(0f, 0f, -0.003f);
            pivote.transform.localRotation = Quaternion.Euler(-VdfPulsadorTablero.GiroPalanca, 0f, 0f);

            var vastago = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            vastago.name = "Vastago";
            vastago.transform.SetParent(pivote.transform, false);
            vastago.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            vastago.transform.localPosition = new Vector3(0f, 0f, -0.016f);
            vastago.transform.localScale = new Vector3(0.008f, 0.016f, 0.008f);
            Quitar(vastago.GetComponent<Collider>());
            VdfPanelBuilder.PintarPublico(vastago, metal, 0.7f);

            var bola = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            bola.name = "Bola";
            bola.transform.SetParent(pivote.transform, false);
            bola.transform.localPosition = new Vector3(0f, 0f, -0.033f);
            bola.transform.localScale = Vector3.one * 0.013f;
            Quitar(bola.GetComponent<Collider>());
            VdfPanelBuilder.PintarPublico(bola, metal, 0.7f);

            // Un solo collider, generoso, en el propio nodo: es el que buscan el rayo y el
            // dedo (VdfXrSetup.PrepararPulsadoresDelTablero toma el primero que encuentra).
            var col = nodo.AddComponent<BoxCollider>();
            col.center = new Vector3(0f, 0f, -0.020f);
            col.size = new Vector3(0.045f, 0.065f, 0.045f);

            var mando = nodo.AddComponent<VdfPulsadorTablero>();
            mando.Accion = accion;

            Texto(nodo.transform, "ON", "ON", new Vector3(0f, 0.026f, -0.001f), new Vector2(0.03f, 0.008f), blanco);
            Texto(nodo.transform, "OFF", "OFF", new Vector3(0f, -0.026f, -0.001f), new Vector2(0.03f, 0.008f), blanco);
            Texto(nodo.transform, "Rotulo", rotulo, new Vector3(0f, -0.048f, -0.001f), new Vector2(0.055f, 0.026f), blanco);
        }

        private static void Quitar(Component c)
        {
            if (c == null) return;
            if (Application.isPlaying) Object.Destroy(c); else Object.DestroyImmediate(c);
        }

        private static GameObject Cubo(Transform padre, string nombre, Vector3 pos, Vector3 escala,
                                       Color color, float suavidad)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = nombre;
            go.transform.SetParent(padre, false);
            go.transform.localPosition = pos;
            go.transform.localScale = escala;
            VdfPanelBuilder.PintarPublico(go, color, suavidad);
            return go;
        }

        private static void Texto(Transform padre, string nombre, string texto, Vector3 pos,
                                  Vector2 caja, Color color)
        {
            var go = new GameObject(nombre);
            go.transform.SetParent(padre, false);
            go.transform.localPosition = pos;

            var tmp = go.AddComponent<TextMeshPro>();
            tmp.text = texto;
            tmp.color = color;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.textWrappingMode = TextWrappingModes.NoWrap;
            tmp.fontStyle = FontStyles.Bold;
            tmp.enableAutoSizing = true;
            tmp.fontSizeMin = 0.02f;
            tmp.fontSizeMax = 1.2f;
            tmp.rectTransform.sizeDelta = caja;
        }
    }
}
