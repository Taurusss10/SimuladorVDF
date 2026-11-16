using NUnit.Framework;
using UnityEngine;
using VDF;

namespace VDF.Tests
{
    /// <summary>
    /// Fase 8: la cámara del modo escritorio.
    ///
    /// Lo que puede salir mal aquí es que la cámara se vaya a donde no se ve nada: por
    /// debajo del banco, por detrás del tablero o metida dentro de él. Los límites son la
    /// parte que hay que fijar.
    /// </summary>
    public class VdfCamaraOrbitalTests
    {
        private GameObject _go;
        private VdfCamaraOrbital _cam;

        [SetUp]
        public void SetUp()
        {
            _go = new GameObject("CamaraPrueba");
            _go.transform.position = new Vector3(0f, 1.62f, -1.35f);
            _cam = _go.AddComponent<VdfCamaraOrbital>();
            _cam.Despertar();   // en Edit Mode no se dispara Start
        }

        [TearDown]
        public void TearDown()
        {
            if (_go != null) Object.DestroyImmediate(_go);
        }

        /// <summary>Deja pasar tiempo hasta que la cámara llega a donde se le pidió.</summary>
        private void Asentar()
        {
            for (int i = 0; i < 120; i++) _cam.Avanzar(0.05f);
        }

        [Test]
        public void ArrancaDondeEstabaLaCamaraDeLaEscena()
        {
            // Si no partiera del encuadre actual, el primer frame daría un salto.
            Assert.AreEqual(Vector3.Distance(new Vector3(0f, 1.62f, -1.35f), _cam.Objetivo),
                            _cam.Distancia, 0.01f);
        }

        [Test]
        public void ElZoomNoSeMeteDentroDelTableroNiSeVaDeLaSala()
        {
            for (int i = 0; i < 200; i++) _cam.Acercar(120f);
            Asentar();
            Assert.AreEqual(_cam.DistanciaMinima, _cam.Distancia, 0.001f,
                "Acercándose sin parar, la cámara acabaría dentro del tablero.");

            for (int i = 0; i < 400; i++) _cam.Acercar(-120f);
            Asentar();
            Assert.AreEqual(_cam.DistanciaMaxima, _cam.Distancia, 0.001f);
        }

        [Test]
        public void LaRuedaDaLoMismoConMuescasDe120QueDe1()
        {
            // Cada medida tiene que partir de cero: Despertar() relee la posición del
            // transform, y si la cámara ya se movió, la segunda medida acumula la primera.
            float conWindows = AcercarYMedir(120f);
            float conUno = AcercarYMedir(1f);

            Assert.AreEqual(conWindows, conUno, 0.001f,
                "El zoom no puede ir a velocidad distinta según el ratón que tenga cada uno.");
        }

        /// <summary>Cuánto se acerca la cámara con una muesca de rueda, partiendo de cero.</summary>
        private float AcercarYMedir(float rueda)
        {
            _go.transform.position = new Vector3(0f, 1.62f, -1.35f);
            _cam.Objetivo = new Vector3(0f, 1.18f, 0.05f);
            _cam.Despertar();

            float antes = _cam.Distancia;
            _cam.Acercar(rueda);
            Asentar();
            return antes - _cam.Distancia;
        }

        [Test]
        public void NoSePuedePasarPorDebajoDelBanco()
        {
            for (int i = 0; i < 500; i++) _cam.Orbitar(0f, -400f);
            Asentar();
            Assert.AreEqual(_cam.ElevacionMaxima, _cam.Elevacion, 0.01f);

            for (int i = 0; i < 500; i++) _cam.Orbitar(0f, 400f);
            Asentar();
            Assert.AreEqual(_cam.ElevacionMinima, _cam.Elevacion, 0.01f);
        }

        [Test]
        public void NoSePuedeRodearElModuloPorDetras()
        {
            // Por detrás no hay nada modelado: el tablero es una placa.
            for (int i = 0; i < 500; i++) _cam.Orbitar(400f, 0f);
            Asentar();
            Assert.AreEqual(_cam.GiroMaximo, _cam.Giro, 0.01f);

            for (int i = 0; i < 1000; i++) _cam.Orbitar(-400f, 0f);
            Asentar();
            Assert.AreEqual(-_cam.GiroMaximo, _cam.Giro, 0.01f);
        }

        [Test]
        public void HayUnaVistaPreparadaPorPractica()
        {
            var v = VdfCamaraOrbital.Vistas();

            Assert.AreEqual(4, v.Length);
            Assert.AreEqual("Conjunto", v[0].Nombre);
            Assert.AreEqual("Teclado V1000", v[1].Nombre);
            Assert.AreEqual("Regleta", v[2].Nombre);
            Assert.AreEqual("Diagrama Ladder", v[3].Nombre);
        }

        [Test]
        public void TodasLasVistasCaenDentroDeLosLimites()
        {
            // Una vista fuera de límites se recorta al saltar a ella, y entonces encuadra
            // otra cosa distinta de la que se diseñó.
            foreach (var v in VdfCamaraOrbital.Vistas())
            {
                Assert.LessOrEqual(Mathf.Abs(v.Giro), _cam.GiroMaximo, v.Nombre + ": giro");
                Assert.GreaterOrEqual(v.Elevacion, _cam.ElevacionMinima, v.Nombre + ": elevación");
                Assert.LessOrEqual(v.Elevacion, _cam.ElevacionMaxima, v.Nombre + ": elevación");
                Assert.GreaterOrEqual(v.Distancia, _cam.DistanciaMinima, v.Nombre + ": distancia");
                Assert.LessOrEqual(v.Distancia, _cam.DistanciaMaxima, v.Nombre + ": distancia");
            }
        }

        [Test]
        public void SaltarAUnaVistaDejaLaCamaraMirandoASuObjetivo()
        {
            // Las que usa IrA: la de arranque y las apuntadas a las piezas de la escena.
            var vistas = _cam.VistasDeLaEscena();

            for (int i = 0; i < vistas.Length; i++)
            {
                _cam.IrAYa(i);

                Vector3 hacia = (vistas[i].Objetivo - _go.transform.position).normalized;
                Assert.Less(Vector3.Angle(_go.transform.forward, hacia), 1f,
                    "La vista '" + vistas[i].Nombre + "' no está mirando a lo que debería.");
                Assert.AreEqual(vistas[i].Distancia,
                    Vector3.Distance(_go.transform.position, vistas[i].Objetivo), 0.01f);
            }
        }

        [Test]
        public void LaCamaraNuncaSeQuedaPorDebajoDelSuelo()
        {
            for (int i = 0; i < VdfCamaraOrbital.Vistas().Length; i++)
            {
                _cam.IrAYa(i);
                Assert.Greater(_go.transform.position.y, 0.05f,
                    "Vista " + i + ": la cámara se hunde en el suelo.");
            }

            // Y tampoco mirando desde lo más bajo que se permite.
            _cam.IrAYa(0);
            for (int i = 0; i < 500; i++) _cam.Orbitar(0f, 400f);
            Asentar();
            Assert.Greater(_go.transform.position.y, 0.05f);
        }

        [Test]
        public void UnaVistaQueNoExiste_NoRevienta()
        {
            Assert.DoesNotThrow(delegate() { _cam.IrA(-1); _cam.IrA(99); });
        }
    }
}
