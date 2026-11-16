using NUnit.Framework;
using UnityEngine;
using VDF;

namespace VDF.Tests
{
    /// <summary>
    /// Los pulsadores del tablero, que hasta ahora eran decorado.
    ///
    /// Estaban modelados con su capuchón y sus borneras, pero solo respondían al teclado: el
    /// alumno los veía, los intentaba pulsar y no pasaba nada. Era la deuda número 3 de la
    /// revisión inicial.
    /// </summary>
    public class VdfPulsadorTableroTests
    {
        private GameObject _raiz;
        private VdfSimulacion _sim;

        [SetUp]
        public void SetUp()
        {
            _raiz = new GameObject("RaizPulsadores");
            _sim = _raiz.AddComponent<VdfSimulacion>();

            Despertar(_sim);
        }

        [TearDown]
        public void TearDown()
        {
            if (_raiz != null) Object.DestroyImmediate(_raiz);
        }

        /// <summary>Llama al Awake del componente, que en Edit Mode no se dispara solo.</summary>
        private static void Despertar(Component c)
        {
            var awake = c.GetType().GetMethod("Awake",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            if (awake != null) awake.Invoke(c, null);
        }

        private VdfPulsadorTablero Pulsador(VdfAccionPulsador accion)
        {
            var go = new GameObject("Pulsador_" + accion);
            go.transform.SetParent(_raiz.transform, false);
            var p = go.AddComponent<VdfPulsadorTablero>();
            p.Accion = accion;
            p.Simulacion = _sim;
            return p;
        }

        [Test]
        public void StartHabilitaElSistema()
        {
            Assert.IsFalse(_sim.Plc.Habilitado);
            Pulsador(VdfAccionPulsador.Start).Pulsar();
            Assert.IsTrue(_sim.Plc.Habilitado);
        }

        [Test]
        public void SubirYBajarMuevenElNivel()
        {
            Pulsador(VdfAccionPulsador.Start).Pulsar();

            var subir = Pulsador(VdfAccionPulsador.Subir);
            subir.Pulsar();
            _sim.Plc.Tick(4f);
            Assert.AreEqual(1, _sim.Plc.NivelActivo);

            subir.Pulsar();
            _sim.Plc.Tick(4f);
            Assert.AreEqual(2, _sim.Plc.NivelActivo);

            Pulsador(VdfAccionPulsador.Bajar).Pulsar();
            _sim.Plc.Tick(4f);
            Assert.AreEqual(1, _sim.Plc.NivelActivo);
        }

        [Test]
        public void StopDetieneYReiniciaLaSeleccion()
        {
            Pulsador(VdfAccionPulsador.Start).Pulsar();
            Pulsador(VdfAccionPulsador.Subir).Pulsar();
            _sim.Plc.Tick(4f);

            Pulsador(VdfAccionPulsador.Stop).Pulsar();

            Assert.IsFalse(_sim.Plc.Habilitado);
            Assert.AreEqual(0, _sim.Plc.NivelActivo);
        }

        [Test]
        public void ElHongoSeEnclavaYLaSegundaPulsacionLoLibera()
        {
            // Es como se comporta el de verdad: se queda metido y hay que soltarlo. Sin esto,
            // el alumno lo pulsa dos veces por instinto y se queda sin saber cómo rearmar.
            var hongo = Pulsador(VdfAccionPulsador.Emergencia);

            hongo.Pulsar();
            Assert.IsTrue(_sim.Plc.EmergenciaActiva, "la primera pulsación corta");

            hongo.Pulsar();
            Assert.IsFalse(_sim.Plc.EmergenciaActiva, "la segunda lo libera");
        }

        [Test]
        public void ElHongoSeQuedaMetidoMientrasLaEmergenciaEsteActiva()
        {
            var hongo = Pulsador(VdfAccionPulsador.Emergencia);
            var seta = new GameObject("Seta");
            seta.transform.SetParent(hongo.transform, false);
            seta.transform.localPosition = new Vector3(0f, 0f, -0.012f);
            hongo.Capuchon = seta.transform;
            Despertar(hongo);

            Vector3 reposo = seta.transform.localPosition;
            hongo.Pulsar();
            Assert.AreNotEqual(reposo.z, seta.transform.localPosition.z, "corta y se queda metido");

            // Por mucho tiempo que pase, no sale solo: lo sujeta el enclavamiento.
            for (int i = 0; i < 20; i++) hongo.Avanzar(1f);
            Assert.AreNotEqual(reposo.z, seta.transform.localPosition.z, "sigue metido");

            hongo.Pulsar();
            Assert.AreEqual(reposo.z, seta.transform.localPosition.z, 0.0001f, "al liberarla, sale");
        }

        [Test]
        public void SinSimulacionEnLaEscena_NoRevienta()
        {
            var go = new GameObject("Suelto");
            try
            {
                var p = go.AddComponent<VdfPulsadorTablero>();
                p.Accion = VdfAccionPulsador.Start;
                p.Simulacion = null;
                Assert.DoesNotThrow(delegate() { p.Pulsar(); });
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void ElCapuchonVuelveASuSitioDespuesDeHundirse()
        {
            var p = Pulsador(VdfAccionPulsador.Start);
            var capuchon = new GameObject("Pulsador");
            capuchon.transform.SetParent(p.transform, false);
            capuchon.transform.localPosition = new Vector3(0f, 0f, -0.009f);
            p.Capuchon = capuchon.transform;
            Despertar(p);                       // aqui es donde se apunta la posicion de reposo

            Vector3 reposo = capuchon.transform.localPosition;
            p.Animar();
            Assert.AreNotEqual(reposo.z, capuchon.transform.localPosition.z, "al pulsar se hunde");

            p.Avanzar(p.Duracion * 0.4f);
            Assert.AreNotEqual(reposo.z, capuchon.transform.localPosition.z, "sigue hundido un instante");

            p.Avanzar(p.Duracion);
            Assert.AreEqual(reposo.z, capuchon.transform.localPosition.z, 0.0001f, "y vuelve solo");
        }
    }
}
