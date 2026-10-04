import type { SvgIconComponent } from '@mui/icons-material'
import SmartToyOutlined from '@mui/icons-material/SmartToyOutlined'
import AccountTreeOutlined from '@mui/icons-material/AccountTreeOutlined'
import MenuBookOutlined from '@mui/icons-material/MenuBookOutlined'
import BuildOutlined from '@mui/icons-material/BuildOutlined'
import ScienceOutlined from '@mui/icons-material/ScienceOutlined'
import PlayCircleOutlineOutlined from '@mui/icons-material/PlayCircleOutlineOutlined'
import FactCheckOutlined from '@mui/icons-material/FactCheckOutlined'
import MonitorHeartOutlined from '@mui/icons-material/MonitorHeartOutlined'
import ExtensionOutlined from '@mui/icons-material/ExtensionOutlined'
import ModelTrainingOutlined from '@mui/icons-material/ModelTrainingOutlined'
import PeopleAltOutlined from '@mui/icons-material/PeopleAltOutlined'
import LockOutlined from '@mui/icons-material/LockOutlined'
import SettingsOutlined from '@mui/icons-material/SettingsOutlined'
import HistoryOutlined from '@mui/icons-material/HistoryOutlined'
import NotificationsActiveOutlined from '@mui/icons-material/NotificationsActiveOutlined'
import PersonOutlined from '@mui/icons-material/PersonOutlined'
import SpaceDashboardOutlined from '@mui/icons-material/SpaceDashboardOutlined'
import AppsOutlined from '@mui/icons-material/AppsOutlined'
import ForumOutlined from '@mui/icons-material/ForumOutlined'
import RocketLaunchOutlined from '@mui/icons-material/RocketLaunchOutlined'
import LanguageOutlined from '@mui/icons-material/LanguageOutlined'
import CodeOutlined from '@mui/icons-material/CodeOutlined'
import CorporateFareOutlined from '@mui/icons-material/CorporateFareOutlined'

const ICONS: Record<string, SvgIconComponent> = {
  Apps: AppsOutlined,
  Forum: ForumOutlined,
  RocketLaunch: RocketLaunchOutlined,
  Language: LanguageOutlined,
  Code: CodeOutlined,
  SmartToy: SmartToyOutlined,
  AccountTree: AccountTreeOutlined,
  MenuBook: MenuBookOutlined,
  Build: BuildOutlined,
  Science: ScienceOutlined,
  PlayCircleOutline: PlayCircleOutlineOutlined,
  FactCheck: FactCheckOutlined,
  MonitorHeart: MonitorHeartOutlined,
  Extension: ExtensionOutlined,
  ModelTraining: ModelTrainingOutlined,
  PeopleAlt: PeopleAltOutlined,
  Lock: LockOutlined,
  Settings: SettingsOutlined,
  History: HistoryOutlined,
  NotificationsActive: NotificationsActiveOutlined,
  Person: PersonOutlined,
  Home: SpaceDashboardOutlined,
  CorporateFare: CorporateFareOutlined,
}

export function getNavIcon(name: string): SvgIconComponent {
  return ICONS[name] ?? SpaceDashboardOutlined
}
