// NOTA IMPORTANTE DE UNITY
// Cada MonoBehaviour tiene que vivir en un archivo .cs con SU MISMO NOMBRE.
// Unity crea un unico MonoScript por archivo, con el nombre del archivo; cualquier
// MonoBehaviour cuya clase no coincida NO obtiene MonoScript y, por tanto, no se puede
// deserializar. Funciona dentro de la sesion (el tipo existe en memoria) y se rompe al
// reiniciar el editor, dejando "The referenced script is missing" en la escena.
// Ver E6 en VIABILIDAD.md: esto costo 71 componentes rotos y, probablemente, un crash.

using UnityEngine;

namespace VDF
{
    /// <summary>
    /// Cable banana para VR: dos extremos agarrables unidos por un cable con física.
    ///
    /// Desde la fase 4 **no dibuja nada por su cuenta**. Antes calculaba su propia parábola,
    /// de modo que el proyecto tenía tres formas distintas de pintar un cable: la del modo
    /// escritorio, la de VR y la de la física. Ahora esta clase solo se ocupa de lo que es
    /// específico de VR —qué conector está agarrado y en qué borne está enchufado— y delega
    /// la forma del cable en <see cref="VdfCablePhysics"/>, igual que el modo escritorio.
    ///
    /// La pieza que lo hace posible es que la física ancla a <c>Transform</c>, no a bornes:
    /// un extremo agarrado por la mano es, para el solucionador, un punto fijo que se mueve.
    /// No hace falta nada especial para VR.
    /// </summary>
    [RequireComponent(typeof(LineRenderer))]
    public class VdfXrCable : MonoBehaviour
    {
        public VdfXrPlug ExtremoA;
        public VdfXrPlug ExtremoB;
        public Color Color = Color.red;

        [Tooltip("Longitud del cable respecto a la distancia entre conectores al crearlo. " +
                 "Por encima de 1 el cable cuelga; a 1 queda tenso como una cuerda.")]
        public float Holgura = 1.35f;

        [SerializeField] private VdfCablePhysics _fisica;

        /// <summary>El cable con física que da la forma. Es el mismo tipo que usa escritorio.</summary>
        public VdfCablePhysics Fisica { get { return _fisica; } }

        public void Inicializar(VdfXrPlug a, VdfXrPlug b, Color color, Material material)
        {
            ExtremoA = a;
            ExtremoB = b;
            Color = color;

            _fisica = GetComponent<VdfCablePhysics>();
            if (_fisica == null) _fisica = gameObject.AddComponent<VdfCablePhysics>();

            _fisica.Color = color;

            float recta = (a != null && b != null)
                ? Vector3.Distance(a.PuntoCable, b.PuntoCable)
                : 0.3f;
            _fisica.Longitud = Mathf.Max(0.12f, recta * Holgura);
            _fisica.Nodos = Mathf.Clamp(Mathf.RoundToInt(_fisica.Longitud / 0.045f) + 4, 8, 24);

            // Los anclajes son los propios conectores: cuando la mano mueve uno, el cable
            // lo sigue sin más. Comparten GameObject y LineRenderer con esta clase.
            _fisica.Inicializar(a != null ? a.transform : null,
                                b != null ? b.transform : null,
                                color, material);

            SincronizarIds();
        }

        private void Awake()
        {
            if (_fisica == null) _fisica = GetComponent<VdfCablePhysics>();
            SincronizarIds();
        }

        /// <summary>
        /// Recalcula el largo del cable para la distancia que tiene que cubrir AHORA.
        ///
        /// Por qué hace falta. El largo se fijaba una sola vez, al nacer el cable en la
        /// bandeja, a partir de la distancia entre sus dos conectores ahí tumbados: unos
        /// 14 cm. Pero despues ese mismo cable se enchufa entre el tablero y el PLC, que
        /// estan a 70. O sea que quedaba estirado CUATRO VECES su longitud, y un cable
        /// estirado al limite no cuelga: se queda recto como una cuerda tirante.
        ///
        /// Es lo que se veia en el visor: veinte lineas rectas cruzando la mesa en diagonal,
        /// que no parecen cables. Recalculando el largo al enchufar, cada cable sale con la
        /// holgura que le toca y cae en catenaria como el del modo escritorio.
        /// </summary>
        public void AjustarLargoAlDestino()
        {
            if (_fisica == null) _fisica = GetComponent<VdfCablePhysics>();
            if (_fisica == null || ExtremoA == null || ExtremoB == null) return;

            float recta = Vector3.Distance(ExtremoA.transform.position, ExtremoB.transform.position);
            if (recta < 0.02f) return;

            float nuevo = Mathf.Max(0.12f, recta * Holgura);
            // Si apenas cambia no se toca: rehacer el cable cuesta y da un salto visual.
            if (Mathf.Abs(nuevo - _fisica.Longitud) < 0.02f) return;

            _fisica.Longitud = nuevo;
            _fisica.Nodos = Mathf.Clamp(Mathf.RoundToInt(nuevo / 0.045f) + 4, 8, 24);
            _fisica.Inicializar(ExtremoA.transform, ExtremoB.transform, Color, null);
        }

        /// <summary>
        /// Copia al cable con física en qué bornes está enchufado.
        ///
        /// Es lo que permite que el cable de VR sea, para todo lo demás, el mismo objeto que
        /// el de escritorio: con <c>IdA</c> e <c>IdB</c> puestos, la validación y el borrado
        /// funcionan igual en los dos modos.
        /// </summary>
        public void SincronizarIds()
        {
            if (_fisica == null) return;

            _fisica.IdA = ExtremoA != null && ExtremoA.Conectado != null ? ExtremoA.Conectado.Id : null;
            _fisica.IdB = ExtremoB != null && ExtremoB.Conectado != null ? ExtremoB.Conectado.Id : null;
        }

        /// <summary>Ids de los bornes donde están enchufados los dos extremos, o null.</summary>
        public bool EstaCompleto(out string idA, out string idB)
        {
            idA = ExtremoA != null && ExtremoA.Conectado != null ? ExtremoA.Conectado.Id : null;
            idB = ExtremoB != null && ExtremoB.Conectado != null ? ExtremoB.Conectado.Id : null;
            return idA != null && idB != null && idA != idB;
        }
    }
}
