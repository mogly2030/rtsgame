using System.Collections.Generic;

using RTSEngine.Entities;
using RTSEngine.Game;

namespace RTSEngine.UnitExtension
{
    public interface IUnitSquadManager : IPostRunGameService, IOptionalGameService
    {
        bool GetSquad(IUnit unit, out IUnitSquad squad);
        bool IsInSquad(IUnit unit);
        bool JoinToNewSquad(IReadOnlyList<IUnit> units);
    }
}