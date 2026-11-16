
using UnityEngine;
using UnityEngine.InputSystem;

namespace VDF
{
    /// <summary>
    /// Cámara orbital del modo escritorio: girar alrededor del módulo, acercarse y
    /// encuadrar los sitios de trabajo con una tecla.
    ///
    /// Por qué hace falta: hasta la fase 8 la cámara de escritorio estaba **clavada**. El
    /// alumno veía el tablero de frente y nada más: no podía acercarse a leer la regleta,
    /// ni mirar el motor, ni ver el diagrama Ladder, que está a un lado. En VR eso lo
    /// resuelve mover la cabeza; en escritorio no lo resolvía nada.
    ///
    /// El reparto de botones está condicionado por lo que ya existía, y eso manda sobre la
    /// costumbre de otros programas: el botón izquierdo señala bornes y el derecho borra
    /// cables desde la fase 4. Así que orbitar va en el **botón central**, que estaba libre.
    /// El teclado tampoco se pisa: Q, W, flechas, espacio y R son los pulsadores del
    /// tablero, y del 1 al 6 los colores de cable, así que las vistas van en F1 a F4.
    /// </summary>
    public class VdfCamaraOrbital : MonoBehaviour
    {
        [Header("Alrededor de qué se orbita")]
        [Tooltip("Centro de la órbita. Por defecto, el centro del tablero.")]
        public Vector3 Objetivo = new Vector3(0f, 1.18f, 0.05f);

        [Header("Límites")]
        public const float DistanciaMinimaPorDefecto = 0.28f;
        public float DistanciaMinima = DistanciaMinimaPorDefecto;
        public float DistanciaMaxima = 3.20f;

        /// <summary>
        /// Hasta dónde se puede bajar y subir la vista.
        ///
        /// No se deja pasar por debajo del banco ni ponerse cenital: desde ahí no se ve el
        /// tablero y el alumno se pierde. Es el equivalente a que el cuerpo no atraviese la
        /// mesa.
        /// </summary>
        public float ElevacionMinima = -12f;
        public float ElevacionMaxima = 62f;

        /// <summary>Hasta dónde se puede rodear el módulo. Por detrás no hay nada modelado.</summary>
        public float GiroMaximo = 78f;

        [Header("Sensibilidad")]
        public float GradosPorPixel = 0.22f;
        public float MetrosPorMuesca = 0.18f;
        public float Suavizado = 12f;

        /// <summary>Una vista preparada, de las que se saltan con F1 a F4.</summary>
        public struct Vista
        {
            public string Nombre;
            public float Giro;
            public float Elevacion;
            public float Distancia;
            public Vector3 Objetivo;

            public Vista(string nombre, float giro, float elevacion, float distancia, Vector3 objetivo)
            {
                Nombre = nombre;
                Giro = giro;
                Elevacion = elevacion;
                Distancia = distancia;
                Objetivo = objetivo;
            }
        }

        /// <summary>
        /// Las cuatro vistas, pensadas para que cada práctica tenga la suya a un toque: el
        /// conjunto, el teclado del V1000 (práctica 2), la regleta (práctica 3) y el
        /// diagrama Ladder (práctica 4).
        ///
        /// La del Ladder se sacó de la geometría y no a ojo: la primera versión enseñaba la
        /// TRASERA del panel, una pantalla negra. El alumno mira el diagrama desde
        /// `posición del panel - su forward * distancia`, que es la misma convención que
        /// usan los otros paneles de la escena.
        /// </summary>
        public static Vista[] Vistas()
        {
            return new Vista[]
            {
                new Vista("Conjunto",          0f, 10f, 1.85f, new Vector3( 0.00f, 1.15f,  0.05f)),
                new Vista("Teclado V1000",    12f,  4f, 0.70f, new Vector3( 0.25f, 1.28f,  0.02f)),
                new Vista("Regleta",          -6f, 16f, 0.75f, new Vector3( 0.00f, 0.88f,  0.00f)),
                new Vista("Diagrama Ladder", -71f,  0f, 1.25f, new Vector3(-1.61f, 1.10f, -0.75f))
            };
        }

        private float _giro;
        private float _elevacion;
        private float _distancia;

        private float _giroDeseado;
        private float _elevacionDeseada;
        private float _distanciaDeseada;
        private Vector3 _objetivoDeseado;

        private bool _arrastrando;

        private void Start()
        {
            Despertar();
        }

        /// <summary>
        /// Toma como punto de partida dónde está puesta la cámara en la escena.
        ///
        /// Así no da un salto en el primer frame y se respeta el encuadre con el que se
        /// montó la escena. Es público y separado de Start por la misma razón que
        /// <c>VdfSimulacion.Despertar</c>: en Edit Mode, AddComponent no dispara Awake ni
        /// Start, y sin esto los tests verían la cámara sin inicializar.
        /// </summary>
        public void Despertar()
        {
            Vector3 relativo = transform.position - Objetivo;
            _distancia = Mathf.Clamp(relativo.magnitude, DistanciaMinima, DistanciaMaxima);

            if (relativo.sqrMagnitude > 0.0001f)
            {
                Vector3 plano = new Vector3(relativo.x, 0f, relativo.z);
                _elevacion = Mathf.Atan2(relativo.y, plano.magnitude) * Mathf.Rad2Deg;
                _giro = Mathf.Atan2(-relativo.x, -relativo.z) * Mathf.Rad2Deg;
            }

            _elevacion = Mathf.Clamp(_elevacion, ElevacionMinima, ElevacionMaxima);
            _giro = Mathf.Clamp(_giro, -GiroMaximo, GiroMaximo);

            _objetivoDeseado = Objetivo;
            _giroDeseado = _giro;
            _elevacionDeseada = _elevacion;
            _distanciaDeseada = _distancia;

            // La vista de arranque es la que encuadra todo el puesto: F1 vuelve a ella.
            _inicial = new Vista("Conjunto", _giro, _elevacion, _distancia, Objetivo);
            _hayInicial = true;
        }

        private Vista _inicial;
        private bool _hayInicial;

        private void Update()
        {
            LeerRaton();
            LeerVistas();
            Avanzar(Time.deltaTime);
        }

        private void LeerRaton()
        {
            var raton = Mouse.current;
            if (raton == null) return;

            // Botón central: el izquierdo señala bornes y el derecho borra cables.
            if (raton.middleButton.wasPressedThisFrame) _arrastrando = true;
            if (raton.middleButton.wasReleasedThisFrame) _arrastrando = false;

            if (_arrastrando)
            {
                Vector2 delta = raton.delta.ReadValue();
                Orbitar(delta.x, delta.y);
            }

            float rueda = raton.scroll.ReadValue().y;
            if (Mathf.Abs(rueda) > 0.01f) Acercar(rueda);
        }

        /// <summary>Gira la vista. En grados por píxel de arrastre.</summary>
        public void Orbitar(float pixelesX, float pixelesY)
        {
            _giroDeseado = Mathf.Clamp(_giroDeseado + pixelesX * GradosPorPixel, -GiroMaximo, GiroMaximo);
            _elevacionDeseada = Mathf.Clamp(_elevacionDeseada - pixelesY * GradosPorPixel,
                                            ElevacionMinima, ElevacionMaxima);
        }

        /// <summary>
        /// Acerca o aleja. El valor que llega es el de la rueda tal cual.
        ///
        /// En Windows la rueda da 120 por muesca y en otros sitios da 1; normalizarlo evita
        /// que el zoom vaya a una velocidad distinta según el ratón que tenga cada uno.
        /// </summary>
        public void Acercar(float rueda)
        {
            float muescas = rueda / 120f;
            if (Mathf.Abs(muescas) < 0.01f) muescas = Mathf.Sign(rueda);

            _distanciaDeseada = Mathf.Clamp(_distanciaDeseada - muescas * MetrosPorMuesca,
                                            DistanciaMinima, DistanciaMaxima);
        }

        private void LeerVistas()
        {
            var t = Keyboard.current;
            if (t == null) return;

            if (t.f1Key.wasPressedThisFrame) IrA(0);
            else if (t.f2Key.wasPressedThisFrame) IrA(1);
            else if (t.f3Key.wasPressedThisFrame) IrA(2);
            else if (t.f4Key.wasPressedThisFrame) IrA(3);
        }

        public Vista[] VistasDeLaEscena()
        {
            var v = Vistas();
            var cam = GetComponent<Camera>();
            float fovV = cam != null ? cam.fieldOfView : 55f;
            float aspecto = cam != null && cam.aspect > 0.1f ? cam.aspect : 16f / 9f;
            float tanV = Mathf.Tan(fovV * 0.5f * Mathf.Deg2Rad);
            float tanH = tanV * aspecto;

            // Lo que hay que encuadrar se mide con sus renderers, como en el reconstructor.
            System.Func<Bounds, float, float> distanciaPara = (b, margen) =>
                Mathf.Max(b.extents.x * margen / tanH, b.extents.y * margen / tanV) + b.extents.z;

            Bounds b2;
            var tablero = GameObject.Find(VdfPanelBuilder.NombreRaiz);
            if (_hayInicial)
                v[0] = _inicial;
            else if (tablero != null && Medir(tablero, out b2))
                v[0] = new Vista("Conjunto", 0f, 14f, Mathf.Max(1.2f, distanciaPara(b2, 2.6f)),
                                 b2.center + new Vector3(0.15f, 0.18f, 0f));

            var teclado = GameObject.Find("AnclajeTeclado");
            if (teclado != null)
                v[1] = new Vista("Teclado V1000", 0f, 8f, 0.32f, teclado.transform.position);

            var bornes = Object.FindObjectsByType<VdfTerminal>();
            if (bornes.Length > 0)
            {
                var b = new Bounds(bornes[0].transform.position, Vector3.zero);
                foreach (var t in bornes) b.Encapsulate(t.transform.position);
                v[2] = new Vista("Regleta y PLC", 0f, 24f, distanciaPara(b, 1.15f), b.center);
            }

            var ladder = GameObject.Find(VdfLadderPanel.NombreRaiz);
            if (ladder != null && Medir(ladder, out b2))
                v[3] = new Vista("Diagrama Ladder", 0f, 0f, distanciaPara(b2, 1.1f), b2.center);

            return v;
        }

        private static bool Medir(GameObject go, out Bounds b)
        {
            b = new Bounds(go.transform.position, Vector3.zero);
            bool hay = false;
            foreach (var r in go.GetComponentsInChildren<Renderer>())
            {
                if (!r.enabled) continue;
                if (!hay) { b = r.bounds; hay = true; } else b.Encapsulate(r.bounds);
            }
            return hay;
        }

        /// <summary>Salta a una de las vistas preparadas, con la animación suave.</summary>
        public void IrA(int indice)
        {
            var vistas = VistasDeLaEscena();
            if (indice < 0 || indice >= vistas.Length) return;

            _giroDeseado = Mathf.Clamp(vistas[indice].Giro, -GiroMaximo, GiroMaximo);
            _elevacionDeseada = Mathf.Clamp(vistas[indice].Elevacion, ElevacionMinima, ElevacionMaxima);
            _distanciaDeseada = Mathf.Clamp(vistas[indice].Distancia, DistanciaMinima, DistanciaMaxima);
            _objetivoDeseado = vistas[indice].Objetivo;
        }

        /// <summary>Deja la cámara en la vista pedida sin animación.</summary>
        public void IrAYa(int indice)
        {
            IrA(indice);
            _giro = _giroDeseado;
            _elevacion = _elevacionDeseada;
            _distancia = _distanciaDeseada;
            Objetivo = _objetivoDeseado;
            Colocar();
        }

        /// <summary>
        /// Acerca la cámara a donde se le ha pedido ir. Pública y con el tiempo por
        /// parámetro, como el Tick del PLC: así se puede probar sin Play Mode.
        /// </summary>
        public void Avanzar(float dt)
        {
            // Suavizado independiente de los fotogramas por segundo: con un Lerp a secas, la
            // camara llegaria antes en un PC rapido que en una Quest.
            float k = Suavizado <= 0f ? 1f : 1f - Mathf.Exp(-Suavizado * dt);

            _giro = Mathf.Lerp(_giro, _giroDeseado, k);
            _elevacion = Mathf.Lerp(_elevacion, _elevacionDeseada, k);
            _distancia = Mathf.Lerp(_distancia, _distanciaDeseada, k);
            Objetivo = Vector3.Lerp(Objetivo, _objetivoDeseado, k);

            Colocar();
        }

        private void Colocar()
        {
            Quaternion rot = Quaternion.Euler(_elevacion, _giro, 0f);
            transform.position = Objetivo + rot * new Vector3(0f, 0f, -_distancia);
            transform.rotation = Quaternion.LookRotation((Objetivo - transform.position).normalized, Vector3.up);
        }

        // --- Estado, para los tests y para la ayuda en pantalla ------------------------------
        public float Giro { get { return _giro; } }
        public float Elevacion { get { return _elevacion; } }
        public float Distancia { get { return _distancia; } }
    }
}
