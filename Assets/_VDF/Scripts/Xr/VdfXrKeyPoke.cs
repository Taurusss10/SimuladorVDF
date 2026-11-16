// NOTA IMPORTANTE DE UNITY
// Cada MonoBehaviour tiene que vivir en un archivo .cs con SU MISMO NOMBRE.
// Unity crea un unico MonoScript por archivo, con el nombre del archivo; cualquier
// MonoBehaviour cuya clase no coincida NO obtiene MonoScript y, por tanto, no se puede
// deserializar. Funciona dentro de la sesion (el tipo existe en memoria) y se rompe al
// reiniciar el editor, dejando "The referenced script is missing" en la escena.
// Ver E6 en VIABILIDAD.md: esto costo 71 componentes rotos y, probablemente, un crash.

using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace VDF
{
    /// <summary>
    /// Hace que una tecla del V1000 se pueda pulsar con el dedo (poke) además de con el ratón.
    /// </summary>
    public class VdfXrKeyPoke : MonoBehaviour
    {
        public VfdKeypadView Vista;
        public VfdKeyButton Boton;

        private XRSimpleInteractable _interactable;

        public void Inicializar(VfdKeypadView vista, VfdKeyButton boton, XRSimpleInteractable interactable)
        {
            Vista = vista;
            Boton = boton;
            Suscribir(interactable);
        }

        /// <summary>
        /// Vuelve a engancharlo todo al arrancar.
        ///
        /// Los listeners de un UnityEvent añadidos por código son NO persistentes: no se
        /// guardan en la escena. Suscribirse solo en Inicializar() (que corre en Edit Mode
        /// al montar la escena) deja la tecla muerta en Play Mode, y además sin ningún
        /// error en consola. Es el mismo fallo que tenían los sockets.
        /// </summary>
        private void Awake()
        {
            if (Vista == null) Vista = Object.FindAnyObjectByType<VfdKeypadView>();
            if (Boton == null) Boton = GetComponent<VfdKeyButton>();
            Suscribir(GetComponent<XRSimpleInteractable>());
        }

        private void Suscribir(XRSimpleInteractable interactable)
        {
            if (interactable == null || _interactable == interactable) return;

            _interactable = interactable;
            _interactable.selectEntered.AddListener(AlPulsar);
            // El GATILLO también: en las acciones por defecto Select es el grip. Ver VdfAjustesMando.
            _interactable.activated.AddListener(AlActivar);
            // Su nombre, en grande, al apuntarlo: el rótulo real no se lee con las gafas.
            _interactable.hoverEntered.AddListener(AlApuntar);
            _interactable.hoverExited.AddListener(AlDejarDeApuntar);
        }

        private void OnDestroy()
        {
            if (_interactable == null) return;
            _interactable.selectEntered.RemoveListener(AlPulsar);
            _interactable.activated.RemoveListener(AlActivar);
            _interactable.hoverEntered.RemoveListener(AlApuntar);
            _interactable.hoverExited.RemoveListener(AlDejarDeApuntar);
        }

        private void AlApuntar(HoverEnterEventArgs args)
        {
            VdfEtiquetaFlotante.Mostrar(this, PuntoBajoElTeclado(), Boton != null ? VdfEtiquetaFlotante.NombreDeTecla(Boton.Tecla) : null,
                                        debajo: true);
        }

        /// <summary>
        /// El centro de la fila de teclas más baja. La etiqueta cuelga de ahí hacia abajo, así
        /// que no tapa ni la pantalla ni otras teclas.
        /// </summary>
        public Vector3 PuntoBajoElTeclado()
        {
            var teclas = transform.parent != null
                ? transform.parent.GetComponentsInChildren<VfdKeyButton>()
                : new VfdKeyButton[0];
            if (teclas.Length == 0) return transform.position;

            float minY = float.MaxValue, minX = float.MaxValue, maxX = float.MinValue;
            Vector3 suma = Vector3.zero;
            foreach (var t in teclas)
            {
                var p = t.transform.position;
                minY = Mathf.Min(minY, p.y);
                minX = Mathf.Min(minX, p.x);
                maxX = Mathf.Max(maxX, p.x);
                suma += p;
            }
            var centro = suma / teclas.Length;
            return new Vector3((minX + maxX) * 0.5f, minY, centro.z);
        }

        private void AlDejarDeApuntar(HoverExitEventArgs args)
        {
            VdfEtiquetaFlotante.Ocultar(this);
        }

        private void AlActivar(ActivateEventArgs args)
        {
            // Clic y vibración: «no escucho nada, no sé si presioné». Ver VdfRespuestaPulsacion.
            VdfRespuestaPulsacion.Dar(transform.position, args != null ? args.interactorObject as Object : null);
            AlPulsar(null);
        }

        /// <summary>Lo mismo que poner el rayo encima. Lo usan los tests.</summary>
        public void Apuntar() { AlApuntar(null); }

        public void DejarDeApuntar() { AlDejarDeApuntar(null); }

        /// <summary>Lo mismo que apretar un gatillo apuntándolo. Lo usan los tests.</summary>
        public void Pulsar()
        {
            AlPulsar(null);
        }

        private void AlPulsar(SelectEnterEventArgs args)
        {
            if (args != null) VdfRespuestaPulsacion.Dar(transform.position, args.interactorObject as Object);
            if (Vista != null && Boton != null) Vista.Pulsar(Boton.Tecla);
        }
    }
}
