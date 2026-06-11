using Jam;
using Zenject;

namespace Game.Installer
{
    public class GameInstaller : MonoInstaller
    {
        public override void InstallBindings()
        {
            Container.Bind<FuncBlock>().AsSingle();
        }
    }
}