// NOTA IMPORTANTE DE UNITY
// Cada MonoBehaviour tiene que vivir en un archivo .cs con SU MISMO NOMBRE.
// Ver E6 en VIABILIDAD.md: esto costo 71 componentes rotos y un crash.

using System.Collections;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace VDF
{
    public class VdfEncajeAlSoltar : MonoBehaviour
    {
        /// <summary>Hasta dónde se busca un borne al soltar, desde la punta.</summary>
        public const float RadioEncaje = 0.03f;

        private XRGrabInteractable _agarre;

        /// <summary>Lo añade a la punta si aún no lo tiene. Idempotente.</summary>
        public static void Asegurar(GameObject punta)
        {
            if (punta.GetComponent<VdfEncajeAlSoltar>() == null) punta.AddComponent<VdfEncajeAlSoltar>();
        }

        private void Awake()
        {
            _agarre = GetComponent<XRGrabInteractable>();
            if (_agarre != null)
            {
                _agarre.selectExited.AddListener(AlSoltar);
                _agarre.selectEntered.AddListener(AlCoger);
            }
        }

        private void OnDestroy()
        {
            if (_agarre == null) return;
            _agarre.selectExited.RemoveListener(AlSoltar);
            _agarre.selectEntered.RemoveListener(AlCoger);
        }

        /// <summary>
        /// Al cogerla con la mano, la punta deja de estar en su borne aunque no la tuviera un
        /// socket (apilada, o puesta por el cableado automático). Antes seguía contando como
        /// enchufada con el cable en la mano.
        /// </summary>
        private void AlCoger(SelectEnterEventArgs args)
        {
            if (args.interactorObject is XRSocketInteractor) return;
            var punta = GetComponent<VdfXrPlug>();
            if (punta == null || punta.Conectado == null) return;
            punta.Conectado = null;
            var puente = Object.FindAnyObjectByType<VdfXrWiring>();
            if (puente != null) puente.Resincronizar();
        }

        private void AlSoltar(SelectExitEventArgs args)
        {
            // Lo suelta la MANO, no un socket (eso es desenchufar), y no por cancelación.
            if (args.isCanceled || args.interactorObject is XRSocketInteractor) return;
            if (isActiveAndEnabled) StartCoroutine(EncajarSiHaceFalta());
        }

        private IEnumerator EncajarSiHaceFalta()
        {
            // Un frame: si el socket la coge por sí mismo, no hay nada que hacer.
            yield return null;
            if (_agarre == null || _agarre.isSelected) yield break;

            Encajar();
        }

        /// <summary>
        /// Lo que pasa al soltar la punta: al borne más cercano, libre u ocupado. Público para
        /// los tests.
        /// </summary>
        public void Encajar()
        {
            if (_agarre == null) _agarre = GetComponent<XRGrabInteractable>();
            var socket = SocketMasCercano(transform.position);
            if (socket == null) return;

            if (!socket.hasSelection)
            {
                var gestor = _agarre.interactionManager;
                socket.socketActive = true;   // por si los bornes estaban en pausa (VdfXrWiring)
                if (gestor != null) gestor.SelectEnter((IXRSelectInteractor)socket, (IXRSelectInteractable)_agarre);
                return;
            }
            Apilar(socket);
        }

        private void Apilar(XRSocketInteractor socket)
        {
            var punta = GetComponent<VdfXrPlug>();
            var marcador = socket.GetComponent<VdfXrSocket>();
            var borne = marcador != null ? marcador.Borne : socket.GetComponentInParent<VdfTerminal>();
            if (punta == null || borne == null) return;

            // Una banana más afuera por cada una que ya haya.
            int debajo = borne.Enchufados;
            transform.position = socket.transform.position - borne.transform.forward * (SaltoApilado * debajo);
            transform.rotation = socket.attachTransform != null ? socket.attachTransform.rotation : socket.transform.rotation;
            punta.Conectado = borne;
            borne.Resaltar(true);
            if (punta.Cable != null) punta.Cable.AjustarLargoAlDestino();
            var puente = marcador != null && marcador.Puente != null ? marcador.Puente : Object.FindAnyObjectByType<VdfXrWiring>();
            if (puente != null) puente.Resincronizar();
        }

        /// <summary>Cuánto sobresale una banana apilada de la de debajo.</summary>
        public const float SaltoApilado = 0.014f;

        /// <summary>El socket activo más cercano a <paramref name="punto"/>, libre u ocupado, dentro del radio.</summary>
        public static XRSocketInteractor SocketMasCercano(Vector3 punto)
        {
            XRSocketInteractor mejor = null;
            float mejorDistancia = RadioEncaje;
            foreach (var s in Object.FindObjectsByType<XRSocketInteractor>())
            {
                if (!s.isActiveAndEnabled) continue;
                float d = Vector3.Distance(s.transform.position, punto);
                if (d < mejorDistancia) { mejorDistancia = d; mejor = s; }
            }
            return mejor;
        }

        /// <summary>El socket libre y activo más cercano a <paramref name="punto"/>, dentro del radio.</summary>
        public static XRSocketInteractor SocketMasCercanoLibre(Vector3 punto)
        {
            XRSocketInteractor mejor = null;
            float mejorDistancia = RadioEncaje;
            foreach (var s in Object.FindObjectsByType<XRSocketInteractor>())
            {
                if (!s.isActiveAndEnabled || s.hasSelection) continue;
                float d = Vector3.Distance(s.transform.position, punto);
                if (d < mejorDistancia) { mejorDistancia = d; mejor = s; }
            }
            return mejor;
        }
    }
}
