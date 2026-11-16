
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace VDF
{
    /// <summary>
    /// Hace que un botón del menú de prácticas se pueda pulsar en VR.
    ///
    /// Por qué hace falta: el menú detecta el clic con `Mouse.current`, y **en un visor no
    /// hay ratón**. En el editor la comprobación engaña, porque `Mouse.current` existe
    /// igualmente; en una Quest el menú quedaba muerto y el alumno con el visor puesto no
    /// podía elegir práctica.
    ///
    /// Es el mismo puente que <see cref="VdfXrKeyPoke"/> hace con las teclas del V1000, y
    /// sirve igual para el dedo y para el rayo del mando: los dos disparan
    /// <c>selectEntered</c> de un <see cref="XRSimpleInteractable"/>.
    /// </summary>
    public class VdfXrBotonMenu : MonoBehaviour
    {
        public VdfBotonPractica Boton;

        private XRSimpleInteractable _interactable;

        public void Inicializar(VdfBotonPractica boton, XRSimpleInteractable interactable)
        {
            Boton = boton;
            Suscribir(interactable);
        }

        private void Awake()
        {
            if (Boton == null) Boton = GetComponent<VdfBotonPractica>();
            Suscribir(GetComponent<XRSimpleInteractable>());
        }

        private void Suscribir(XRSimpleInteractable interactable)
        {
            if (interactable == null || _interactable == interactable) return;

            _interactable = interactable;
            _interactable.selectEntered.AddListener(AlPulsar);
            // El GATILLO también: en las acciones por defecto Select es el grip. Ver VdfAjustesMando.
            _interactable.activated.AddListener(AlActivar);
        }

        private void OnDestroy()
        {
            if (_interactable == null) return;
            _interactable.selectEntered.RemoveListener(AlPulsar);
            _interactable.activated.RemoveListener(AlActivar);
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
            if (Boton != null) Boton.Pulsar();
        }
    }
}
