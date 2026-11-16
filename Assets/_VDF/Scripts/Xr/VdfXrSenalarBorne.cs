
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace VDF
{
    /// <summary>
    /// Permite señalar un borne con el rayo del mando o con el dedo, en VR.
    ///
    /// Por qué hace falta: los bornes ya tenían un <c>XRSocketInteractor</c>, pero eso sirve
    /// para **recibir** un conector, no para que el alumno los **señale**. Señalar es lo que
    /// pide la práctica 1 (reconocimiento), y en escritorio lo resuelve el raycast de
    /// <see cref="VdfJumperController"/> con el ratón. En un visor no hay ratón, así que la
    /// práctica 1 no se podía hacer con el visor puesto.
    ///
    /// Entra por el mismo sitio que el clic: <see cref="VdfJumperController.SenalarBorne"/>,
    /// que es el punto de entrada único. Así el comportamiento es idéntico en los dos modos,
    /// incluido el no crear cables cuando la práctica no lo permite.
    /// </summary>
    public class VdfXrSenalarBorne : MonoBehaviour
    {
        public VdfTerminal Borne;
        public VdfJumperController Cableado;

        private XRSimpleInteractable _interactable;

        public void Inicializar(VdfTerminal borne, VdfJumperController cableado,
                                XRSimpleInteractable interactable)
        {
            Borne = borne;
            Cableado = cableado;
            Suscribir(interactable);
        }

        private void Awake()
        {
            if (Borne == null) Borne = GetComponent<VdfTerminal>();
            if (Cableado == null) Cableado = Object.FindAnyObjectByType<VdfJumperController>();
            Suscribir(GetComponent<XRSimpleInteractable>());
        }

        private void Suscribir(XRSimpleInteractable interactable)
        {
            if (interactable == null || _interactable == interactable) return;

            _interactable = interactable;
            _interactable.selectEntered.AddListener(AlSenalar);
            // El GATILLO también: en las acciones por defecto Select es el grip. Ver VdfAjustesMando.
            _interactable.activated.AddListener(AlActivar);

            // Y el APUNTADO, que es lo que faltaba.
            _interactable.hoverEntered.AddListener(AlApuntar);
            _interactable.hoverExited.AddListener(AlDejarDeApuntar);
        }

        private void OnDestroy()
        {
            if (_interactable == null) return;
            _interactable.selectEntered.RemoveListener(AlSenalar);
            _interactable.activated.RemoveListener(AlActivar);
            _interactable.hoverEntered.RemoveListener(AlApuntar);
            _interactable.hoverExited.RemoveListener(AlDejarDeApuntar);
        }

        private void AlActivar(ActivateEventArgs args)
        {
            // Clic y vibración: «no escucho nada, no sé si presioné». Ver VdfRespuestaPulsacion.
            VdfRespuestaPulsacion.Dar(transform.position, args != null ? args.interactorObject as Object : null);
            AlSenalar(null);
        }

        private void AlSenalar(SelectEnterEventArgs args)
        {
            if (args != null) VdfRespuestaPulsacion.Dar(transform.position, args.interactorObject as Object);
            Senalar();
        }

        /// <summary>Lo mismo que pasar el rayo por encima. Lo usan los tests.</summary>
        public void Apuntar() { AlApuntar(null); }

        /// <summary>Lo mismo que quitar el rayo de encima. Lo usan los tests.</summary>
        public void DejarDeApuntar() { AlDejarDeApuntar(null); }

        /// <summary>Lo mismo que apretar el gatillo apuntando al borne.</summary>
        public void Senalar()
        {
            // IDENTIFICAR, no SenalarBorne. SenalarBorne es el clic del ratón: en las
            // prácticas que dejan cablear, dos clics en dos bornes TIENDEN un cable. En el
            // visor los cables se cogen con la mano, así que dos gatillazos creaban un cable
            // fantasma que el visor no muestra, y la práctica 3 se podía «completar» sin
            // tocar un solo cable.
            if (Cableado != null && Borne != null) Cableado.IdentificarBorne(Borne);
        }

        private void AlApuntar(HoverEnterEventArgs args)
        {
            if (Borne != null) Borne.Resaltar(true);
            // El rótulo del tablero no se lee con las gafas: ver VdfEtiquetaFlotante.
            if (Borne != null)
                VdfEtiquetaFlotante.Mostrar(this, Borne.transform.position, VdfEtiquetaFlotante.NombreDeBorne(Borne));
        }

        private void AlDejarDeApuntar(HoverExitEventArgs args)
        {
            VdfEtiquetaFlotante.Ocultar(this);

            // Si el borne quedo resaltado porque tiene algo enchufado, no se apaga aqui:
            // ese resaltado lo gobierna VdfXrSocket y tiene otro significado.
            if (Borne == null) return;
            if (Borne.TieneCableEnchufado) return;
            Borne.Resaltar(false);
        }
    }
}
