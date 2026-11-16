using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace VDF
{
    /// <summary>
    /// Cableado con ratón en modo escritorio.
    ///
    /// Clic izquierdo en un borne lo selecciona; clic izquierdo en un segundo borne crea
    /// el cable. Clic derecho sobre un cable lo borra; clic derecho sobre un borne borra
    /// todos los cables que llegan a él. Las teclas 1–6 eligen el color.
    ///
    /// Ojo con el Input: este proyecto tiene activeInputHandler = 1, o sea SOLO el Input
    /// System nuevo. UnityEngine.Input clásico lanzaría excepción en Play Mode, así que
    /// todo va por Mouse.current / Keyboard.current.
    ///
    /// El controlador solo se ocupa de la interacción y del dibujo. Quién está conectado
    /// con quién lo guarda <see cref="VdfWiringGraph"/>, que es C# puro: la misma verdad
    /// sirve para escritorio y para VR.
    /// </summary>
    public class VdfJumperController : MonoBehaviour, IVdfFuenteCableado
    {
        [Header("Referencias")]
        [Tooltip("Cámara desde la que se lanza el rayo. Si se deja vacío, usa Camera.main.")]
        public Camera Camara;

        [Tooltip("Dónde se cuelgan los cables creados. Si se deja vacío, cuelgan de este objeto.")]
        public Transform ContenedorCables;

        [Header("Colores de cable (teclas 1-6)")]
        public Color[] Paleta =
        {
            new Color(0.85f, 0.15f, 0.13f),  // rojo
            new Color(0.10f, 0.10f, 0.11f),  // negro
            new Color(0.15f, 0.38f, 0.78f),  // azul
            new Color(0.16f, 0.65f, 0.25f),  // verde
            new Color(0.93f, 0.82f, 0.12f),  // amarillo
            new Color(0.80f, 0.81f, 0.83f)   // gris
        };

        [Header("Estado")]
        [SerializeField] private int _colorActual;

        private VdfWiringGraph _grafo;

        public VdfWiringGraph Grafo
        {
            get
            {
                if (_grafo == null) _grafo = new VdfWiringGraph();
                return _grafo;
            }
        }

        /// <summary>Borne seleccionado a la espera del segundo clic. null si no hay ninguno.</summary>
        public VdfTerminal Seleccionado { get; private set; }

        public int ColorActual
        {
            get { return _colorActual; }
            set { _colorActual = Paleta.Length == 0 ? 0 : ((value % Paleta.Length) + Paleta.Length) % Paleta.Length; }
        }

        public Color ColorDeCable
        {
            get { return Paleta.Length == 0 ? Color.white : Paleta[ColorActual]; }
        }

        /// <summary>Cables dibujados, en paralelo al grafo.</summary>
        private readonly List<VdfCablePhysics> _cables = new List<VdfCablePhysics>();

        private readonly Dictionary<string, VdfTerminal> _bornes = new Dictionary<string, VdfTerminal>();
        /// <summary>
        /// Un material por color de cable.
        ///
        /// No basta con poner startColor/endColor en el LineRenderer: el shader
        /// URP/Unlit NO multiplica por color de vértice, así que todos los cables salían
        /// del color del material (blanco) por mucho que el LineRenderer dijera otra cosa.
        /// Se cachea por color para no crear un material por cable.
        /// </summary>
        private readonly Dictionary<string, Material> _materialesCable = new Dictionary<string, Material>();

        private Shader _shaderCable;
        private VdfCablePhysics _cableBajoCursor;

        /// <summary>Se dispara cuando el cableado cambia, para refrescar la validación en pantalla.</summary>
        public event System.Action CableadoCambiado;

        /// <summary>
        /// Se dispara al señalar un borne, se cablee o no.
        ///
        /// Lo escucha el sistema de prácticas: la práctica 1 pide identificar componentes, y
        /// señalar un borne es exactamente eso. El controlador no sabe nada de prácticas; se
        /// limita a avisar de que el alumno ha señalado algo.
        /// </summary>
        public event System.Action<VdfTerminal> BorneSenalado;

        /// <summary>
        /// Si está en false, señalar bornes solo los resalta: no crea cables.
        ///
        /// Hace falta para la práctica de reconocimiento, donde el alumno va tocando
        /// componentes para identificarlos y no debería acabar con el tablero lleno de
        /// cables puestos sin querer.
        /// </summary>
        public bool PermitirCablear = true;

        private void Awake()
        {
            if (Camara == null) Camara = Camera.main;
            if (ContenedorCables == null) ContenedorCables = transform;

            IndexarBornes();
            PrepararMaterial();
        }

        /// <summary>
        /// Recorre la escena y guarda todos los bornes por Id. Incluye los del tablero,
        /// los del PLC y los del motor: para el grafo todos son iguales.
        /// </summary>
        public void IndexarBornes()
        {
            _bornes.Clear();

            var encontrados = Object.FindObjectsByType<VdfTerminal>();
            for (int i = 0; i < encontrados.Length; i++)
            {
                var t = encontrados[i];
                if (string.IsNullOrEmpty(t.Id)) continue;

                if (_bornes.ContainsKey(t.Id))
                {
                    Debug.LogWarning("[VDF] Borne con Id repetido en la escena: " + t.Id, t);
                    continue;
                }
                _bornes[t.Id] = t;
            }
        }

        public int BornesIndexados { get { return _bornes.Count; } }

        public VdfTerminal BuscarBorne(string id)
        {
            // Un id nulo devuelve null, no revienta. Dictionary.TryGetValue lanza
            // ArgumentNullException con una clave nula, y eso convertia un «no lo encuentro»
            // en una excepcion que tumbaba lo que estuviera corriendo. Como este metodo lo
            // llaman el raton, el rayo de VR y los guiones de las practicas, cualquiera de
            // esos caminos podia pasar un id vacio y llevarse por delante el fotograma.
            if (string.IsNullOrEmpty(id)) return null;

            VdfTerminal t;
            return _bornes.TryGetValue(id, out t) ? t : null;
        }

        private void PrepararMaterial()
        {
            _shaderCable = Shader.Find("Universal Render Pipeline/Unlit");
            if (_shaderCable == null) _shaderCable = Shader.Find("Unlit/Color");
            if (_shaderCable == null) _shaderCable = Shader.Find("Sprites/Default");
        }

        /// <summary>Material unlit del color pedido, compartido entre cables del mismo color.</summary>
        public Material MaterialParaColor(Color color)
        {
            if (_shaderCable == null) PrepararMaterial();

            string clave = ColorUtility.ToHtmlStringRGB(color);

            Material mat;
            if (_materialesCable.TryGetValue(clave, out mat) && mat != null) return mat;

            mat = new Material(_shaderCable) { name = "VDF_Cable_" + clave };
            mat.SetColor("_BaseColor", color);
            mat.SetColor("_Color", color);
            _materialesCable[clave] = mat;
            return mat;
        }

        // =====================================================================================
        // Bucle de entrada
        // =====================================================================================

        private void Update()
        {
            var raton = Mouse.current;
            if (raton == null) return;

            LeerTeclasDeColor();

            VdfTerminal borne;
            VdfCablePhysics cable;
            Apuntar(raton.position.ReadValue(), out borne, out cable);

            ResaltarCableBajoCursor(cable);
            NombrarBorneBajoCursor(borne);

            if (raton.leftButton.wasPressedThisFrame)
            {
                if (borne != null) SenalarBorne(borne);
                else Deseleccionar();
            }
            else if (raton.rightButton.wasPressedThisFrame)
            {
                if (cable != null) BorrarCable(cable);
                else if (borne != null) BorrarCablesDe(borne.Id);
            }
        }

        private void LeerTeclasDeColor()
        {
            var teclado = Keyboard.current;
            if (teclado == null) return;

            if (teclado.digit1Key.wasPressedThisFrame) ColorActual = 0;
            else if (teclado.digit2Key.wasPressedThisFrame) ColorActual = 1;
            else if (teclado.digit3Key.wasPressedThisFrame) ColorActual = 2;
            else if (teclado.digit4Key.wasPressedThisFrame) ColorActual = 3;
            else if (teclado.digit5Key.wasPressedThisFrame) ColorActual = 4;
            else if (teclado.digit6Key.wasPressedThisFrame) ColorActual = 5;
            else if (teclado.escapeKey.wasPressedThisFrame) Deseleccionar();
        }

        private void Apuntar(Vector2 posicionPantalla, out VdfTerminal borne, out VdfCablePhysics cable)
        {
            borne = null;
            cable = null;
            if (Camara == null) return;

            Ray rayo = Camara.ScreenPointToRay(posicionPantalla);
            RaycastHit[] impactos = Physics.RaycastAll(rayo, 50f, ~0, QueryTriggerInteraction.Collide);
            if (impactos.Length == 0) return;

            System.Array.Sort(impactos, (x, y) => x.distance.CompareTo(y.distance));

            // Los impactos vienen ordenados de cerca a lejos, asi que esta lista conserva
            // ese orden y sirve de desempate en ElegirBorne.
            _tocados.Clear();

            for (int i = 0; i < impactos.Length; i++)
            {
                var t = impactos[i].collider.GetComponentInParent<VdfTerminal>();
                if (t != null)
                {
                    if (!_tocados.Contains(t)) _tocados.Add(t);
                    continue;
                }

                if (cable == null)
                {
                    // GetComponentInParent y no GetComponent: desde la fase 4 el volumen de
                    // seleccion son varias capsulas HIJAS que siguen la curva del cable.
                    var j = impactos[i].collider.GetComponentInParent<VdfCablePhysics>();
                    if (j != null) cable = j;
                }
            }

            borne = ElegirBorne(rayo, _tocados);
        }

        /// <summary>Reutilizada cada frame para no generar basura en el bucle de entrada.</summary>
        private readonly List<VdfTerminal> _tocados = new List<VdfTerminal>();

        /// <summary>
        /// De entre los bornes que toca el rayo, el que el usuario cree que esta señalando:
        /// el de centro mas cercano a la recta del rayo.
        ///
        /// La lista se espera ordenada de cerca a lejos; con desvios practicamente iguales
        /// (menos de medio milimetro de diferencia) gana el primero, o sea el mas cercano a
        /// la camara. Asi dos bornes alineados a la vista se comportan como antes.
        /// </summary>
        public static VdfTerminal ElegirBorne(Ray rayo, IList<VdfTerminal> tocados)
        {
            if (tocados == null) return null;

            VdfTerminal mejor = null;
            float mejorDesvio = float.MaxValue;

            for (int i = 0; i < tocados.Count; i++)
            {
                if (tocados[i] == null) continue;

                float desvio = DistanciaAlRayo(rayo, tocados[i].transform.position);
                if (mejor == null || desvio < mejorDesvio - 0.0005f)
                {
                    mejor = tocados[i];
                    mejorDesvio = desvio;
                }
            }

            return mejor;
        }

        /// <summary>Distancia perpendicular de un punto a la recta del rayo.</summary>
        private static float DistanciaAlRayo(Ray rayo, Vector3 punto)
        {
            Vector3 v = punto - rayo.origin;
            float avance = Vector3.Dot(v, rayo.direction);
            Vector3 enLaRecta = rayo.origin + rayo.direction * avance;
            return Vector3.Distance(punto, enLaRecta);
        }

        private void NombrarBorneBajoCursor(VdfTerminal borne)
        {
            if (borne == _borneBajoCursor) return;
            _borneBajoCursor = borne;
            if (borne != null)
                VdfEtiquetaFlotante.Mostrar(this, borne.transform.position, VdfEtiquetaFlotante.NombreDeBorne(borne));
            else
                VdfEtiquetaFlotante.Ocultar(this);
        }

        private VdfTerminal _borneBajoCursor;

        private void ResaltarCableBajoCursor(VdfCablePhysics cable)
        {
            if (_cableBajoCursor == cable) return;
            if (_cableBajoCursor != null) _cableBajoCursor.Resaltar(false);
            _cableBajoCursor = cable;
            if (_cableBajoCursor != null) _cableBajoCursor.Resaltar(true);
        }

        // =====================================================================================
        // Acciones
        // =====================================================================================

        /// <summary>
        /// Señala un borne. Es el punto de entrada único: lo llama el clic del ratón en
        /// escritorio y lo llamará el rayo o el dedo en VR, igual que <see cref="Conectar"/>
        /// lo es para crear cables. Público también para poder probarlo sin ratón.
        /// </summary>
        public void SenalarBorne(VdfTerminal borne)
        {
            if (borne == null) return;

            var aviso = BorneSenalado;
            if (aviso != null) aviso(borne);

            if (!PermitirCablear)
            {
                // Solo identificar: se resalta un instante para dar acuse de recibo y se
                // suelta, de modo que no quede un borne «a medio cablear».
                Seleccionar(borne);
                Deseleccionar();
                return;
            }

            if (Seleccionado == null)
            {
                Seleccionar(borne);
                return;
            }

            if (Seleccionado == borne)
            {
                Deseleccionar();
                return;
            }

            Conectar(Seleccionado.Id, borne.Id, ColorDeCable);
            Deseleccionar();
        }

        /// <summary>
        /// Señala un borne SOLO para identificarlo, aunque la práctica deje cablear. Es lo
        /// que hace el gatillo en el visor: allí los cables se tienden con la mano.
        /// </summary>
        public void IdentificarBorne(VdfTerminal borne)
        {
            if (borne == null) return;

            var aviso = BorneSenalado;
            if (aviso != null) aviso(borne);

            if (Seleccionado != null) Deseleccionar();
            Seleccionar(borne);
            Deseleccionar();
        }

        private void Seleccionar(VdfTerminal borne)
        {
            Seleccionado = borne;
            borne.Resaltar(true);
        }

        public void Deseleccionar()
        {
            if (Seleccionado != null) Seleccionado.Resaltar(false);
            Seleccionado = null;
        }

        /// <summary>
        /// Crea un cable entre dos bornes. Devuelve false si no se puede (bucle, duplicado
        /// o algún Id inexistente). Es el punto de entrada único: lo usa el ratón, lo usará
        /// el agarre XR de E6 y lo usan los tests.
        /// </summary>
        public bool Conectar(string idA, string idB, Color color)
        {
            VdfTerminal a = BuscarBorne(idA);
            VdfTerminal b = BuscarBorne(idB);
            if (a == null || b == null) return false;

            if (!Grafo.Conectar(idA, idB)) return false;

            var cable = VdfCableSpawner.Tender(a, b, color, ContenedorCables);
            if (cable == null)
            {
                // Si no se pudo tender, no dejar el grafo mintiendo.
                Grafo.Desconectar(idA, idB);
                return false;
            }

            cable.IdA = idA;
            cable.IdB = idB;
            _cables.Add(cable);

            Avisar();
            return true;
        }

        public bool Conectar(string idA, string idB)
        {
            return Conectar(idA, idB, ColorDeCable);
        }

        /// <summary>
        /// Dispara el aviso de cableado cambiado desde fuera.
        ///
        /// Lo usa el puente XR: en VR los cables los enchufa el alumno en los sockets, no
        /// este controlador, pero quien escucha para revalidar (VdfSimulacion) está
        /// suscrito aquí. Sin esto, el informe de cableado no se actualizaba en VR.
        /// </summary>
        public void AvisarCableadoCambiado()
        {
            Avisar();
        }

        public void BorrarCable(VdfCablePhysics cable)
        {
            if (cable == null) return;

            Grafo.Desconectar(cable.IdA, cable.IdB);
            _cables.Remove(cable);
            if (_cableBajoCursor == cable) _cableBajoCursor = null;

            if (Application.isPlaying) Destroy(cable.gameObject);
            else DestroyImmediate(cable.gameObject);

            Avisar();
        }

        /// <summary>Quita todos los cables que llegan a un borne. Devuelve cuántos quitó.</summary>
        public int BorrarCablesDe(string id)
        {
            var aBorrar = _cables.FindAll(c => c.Toca(id));
            for (int i = 0; i < aBorrar.Count; i++) BorrarCable(aBorrar[i]);
            return aBorrar.Count;
        }

        public void BorrarTodo()
        {
            for (int i = _cables.Count - 1; i >= 0; i--) BorrarCable(_cables[i]);
            Grafo.Limpiar();
            Avisar();
        }

        public int CablesDibujados { get { return _cables.Count; } }

        /// <summary>Dibuja de golpe el cableado correcto. Para el modo "saltar cableado" y para probar.</summary>
        public int CablearCorrectamente()
        {
            var redes = VdfWiringSpec.Practica();
            int puestos = 0;

            for (int i = 0; i < redes.Count; i++)
            {
                var red = redes[i];
                for (int j = 0; j + 1 < red.Bornes.Length; j++)
                {
                    if (Conectar(red.Bornes[j], red.Bornes[j + 1], Paleta[i % Paleta.Length]))
                        puestos++;
                }
            }

            // Una pasada final con TODOS a la vez. Cada cable se asienta al nacer, asi que
            // sin esto los primeros nunca llegan a verse con los ultimos y el mazo queda
            // atravesado. Avanzandolos en paralelo se ordenan y acaban apoyados unos sobre
            // otros, como en una mesa de verdad.
            VdfCablePhysics.AsentarTodos();

            return puestos;
        }

        public VdfWiringReport Validar()
        {
            return VdfWiringSpec.Validar(Grafo);
        }

        private void Avisar()
        {
            var h = CableadoCambiado;
            if (h != null) h();
        }
    }
}
