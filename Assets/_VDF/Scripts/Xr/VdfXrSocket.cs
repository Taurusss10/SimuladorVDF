// NOTA IMPORTANTE DE UNITY
// Cada MonoBehaviour tiene que vivir en un archivo .cs con SU MISMO NOMBRE.
// Unity crea un unico MonoScript por archivo, con el nombre del archivo; cualquier
// MonoBehaviour cuya clase no coincida NO obtiene MonoScript y, por tanto, no se puede
// deserializar. Funciona dentro de la sesion (el tipo existe en memoria) y se rompe al
// reiniciar el editor, dejando "The referenced script is missing" en la escena.
// Ver E6 en VIABILIDAD.md: esto costo 71 componentes rotos y, probablemente, un crash.

using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace VDF
{
    /// <summary>
    /// Convierte un borne en un socket XR: acepta conectores y avisa al puente.
    /// </summary>
    public class VdfXrSocket : MonoBehaviour
    {
        public VdfTerminal Borne;
        public VdfXrWiring Puente;

        private XRSocketInteractor _socket;

        public void Inicializar(VdfTerminal borne, VdfXrWiring puente, XRSocketInteractor socket)
        {
            Borne = borne;
            Puente = puente;
            Suscribir(socket);
        }

        /// <summary>
        /// Mete una punta en este borne por la vía de XRI, la misma que al soltarla encima.
        /// Para los tests, que no ven los tipos de XRI. Devuelve si quedó enchufada.
        /// </summary>
        public bool Recibir(VdfXrPlug punta)
        {
            if (_socket == null) _socket = GetComponent<XRSocketInteractor>();
            var agarre = punta != null ? punta.GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable>() : null;
            if (_socket == null || agarre == null || _socket.interactionManager == null) return false;
            punta.transform.position = transform.position;
            // Si el puente acaba de soltar los cables, los bornes están en pausa (ver
            // VdfXrWiring.ApagarSocketsUnMomento). Este se enciende ya: se le está dando una
            // punta concreta, no se deja que elija entre lo que cree tener encima.
            _socket.socketActive = true;
            _socket.interactionManager.SelectEnter((IXRSelectInteractor)_socket,
                (UnityEngine.XR.Interaction.Toolkit.Interactables.IXRSelectInteractable)agarre);
            return punta.Conectado == Borne;
        }

        private void Awake()
        {
            // Tras recargar la escena, los eventos hay que volver a engancharlos.
            if (_socket == null) Suscribir(GetComponent<XRSocketInteractor>());
            if (Borne == null) Borne = GetComponentInParent<VdfTerminal>();
            if (Puente == null) Puente = Object.FindAnyObjectByType<VdfXrWiring>();
        }

        private void Suscribir(XRSocketInteractor socket)
        {
            if (socket == null || _socket == socket) return;

            _socket = socket;
            _socket.selectEntered.AddListener(AlEnchufar);
            _socket.selectExited.AddListener(AlDesenchufar);
        }

        private void OnDestroy()
        {
            if (_socket == null) return;
            _socket.selectEntered.RemoveListener(AlEnchufar);
            _socket.selectExited.RemoveListener(AlDesenchufar);
        }

        private void AlEnchufar(SelectEnterEventArgs args)
        {
            var plug = args.interactableObject.transform.GetComponent<VdfXrPlug>();
            if (plug == null) return;

            plug.Conectado = Borne;
            if (Borne != null) Borne.Resaltar(true);

            // El cable que acaba de engancharse tiene que dar un salto distinto al que daba
            // en la bandeja, asi que se recalcula su largo. Si no, sale recto y tirante.
            if (plug.Cable != null) plug.Cable.AjustarLargoAlDestino();
            if (Puente != null) Puente.Resincronizar();
        }

        private void AlDesenchufar(SelectExitEventArgs args)
        {
            var plug = args.interactableObject.transform.GetComponent<VdfXrPlug>();
            if (plug == null) return;

            if (plug.Conectado == Borne) plug.Conectado = null;
            if (Borne != null)
            {
                if (!Borne.TieneCableEnchufado) Borne.Resaltar(false);
            }
            if (Puente != null) Puente.Resincronizar();
        }
    }
}
