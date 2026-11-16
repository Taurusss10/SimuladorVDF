// NOTA IMPORTANTE DE UNITY
// Cada MonoBehaviour tiene que vivir en un archivo .cs con SU MISMO NOMBRE.
// Unity crea un unico MonoScript por archivo, con el nombre del archivo; cualquier
// MonoBehaviour cuya clase no coincida NO obtiene MonoScript y, por tanto, no se puede
// deserializar. Ver E6 en VIABILIDAD.md: esto costo 71 componentes rotos y un crash.

using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace VDF
{
    public class VdfXrPulsador : MonoBehaviour
    {
        public VdfPulsadorTablero Pulsador;

        private XRSimpleInteractable _interactable;

        public void Inicializar(VdfPulsadorTablero pulsador, XRSimpleInteractable interactable)
        {
            Pulsador = pulsador;
            Suscribir(interactable);
        }

        /// <summary>
        /// Se vuelve a enganchar al arrancar.
        ///
        /// Los listeners de un UnityEvent añadidos por código **no se guardan en la escena**.
        /// Suscribirse solo en Inicializar —que corre en el editor al montar— deja el
        /// pulsador mudo en Play y en el instalable, y sin un solo error en consola. Es el
        /// mismo fallo que ya tuvieron los sockets y las teclas del V1000.
        /// </summary>
        private void Awake()
        {
            if (Pulsador == null) Pulsador = GetComponentInParent<VdfPulsadorTablero>();
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
            VdfEtiquetaFlotante.Mostrar(this, transform.position, VdfEtiquetaFlotante.NombreDePulsador(Pulsador != null ? Pulsador.name : name));
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

        /// <summary>Lo mismo que apretar un gatillo apuntándolo. Lo usan los tests.</summary>
        public void Pulsar()
        {
            AlPulsar(null);
        }

        private void AlPulsar(SelectEnterEventArgs args)
        {
            if (args != null) VdfRespuestaPulsacion.Dar(transform.position, args.interactorObject as Object);
            if (Pulsador != null) Pulsador.Pulsar();
        }
    }
}
