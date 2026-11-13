export type Locale = 'zh-CN' | 'en';
export type Bilingual = Record<Locale, string>;
export type PublishedText = Partial<Bilingual>;
export type PublishedLocales<T> = Partial<Record<Locale, T>>;
export type Knowledge<T> = { knowledgeState: 'known'; value: T; editorialState?: 'checked'|'needsReview' } | { knowledgeState: 'unknown' | 'notApplicable'; value: null; editorialState?: 'checked'|'needsReview' };
export const known = <T>(value: T): Knowledge<T> => ({ knowledgeState: 'known', value });
export const unknown = <T>(): Knowledge<T> => ({ knowledgeState: 'unknown', value: null });
export const notApplicable = <T>(): Knowledge<T> => ({ knowledgeState: 'notApplicable', value: null });
export type Range = { min: number; max: number };
export type EntityKind = 'fish' | 'shrimp' | 'snail' | 'plant' | 'rock' | 'sand' | 'wood' | 'substrate';
export type WaterType = 'freshwater' | 'marine' | 'brackish';
export type Flow = 'minimal' | 'gentle' | 'moderate' | 'strong';
export type Light = 'low' | 'medium' | 'high';
export interface EntityLocale { name: string; summary: string; description: string; alt: string }
export interface FeedingProfile {
  pelletDiameterMm: Range | null;
  foodForm: 'micro-pellet' | 'pellet' | 'sinking-pellet' | 'wafer';
  lifeStage: 'adult';
  advice: Bilingual;
}
export interface CareProfile {
  feeding?: FeedingProfile;
  adultLengthMm: Knowledge<number>; temperatureC: Knowledge<Range>; ph: Knowledge<Range>; gh: Knowledge<Range>;
  minimumTankL: Knowledge<number>; minimumTankLengthMm: Knowledge<number>;
  group: Knowledge<{ mode: 'shoal' | 'colony' | 'solitary' | 'conditional_pair' | 'independent'; minimum: number; recommended: number }>;
  waterLayers: Knowledge<string[]>; oxygen: Knowledge<'standard' | 'high'>; flow: Knowledge<Flow[]>;
  diet: Knowledge<string>; temperament: Knowledge<string>; difficulty: Knowledge<string>;
  notes: PublishedText; editorialState?: Partial<Record<keyof Omit<CareProfile, 'editorialState'>, 'checked' | 'needsReview'>>; legacyDataStatus?: 'ready' | 'partial' | 'unavailable';
}
export interface PlantProfile { submerged: Knowledge<boolean>; placement: ('foreground' | 'midground' | 'background' | 'floating')[]; attachment: 'epiphyte' | 'rooted' | 'floating'; light: Knowledge<Light[]>; co2: Knowledge<'optional' | 'recommended' | 'required' | 'none'>; adultHeightMm: Range; adultWidthMm: Range; unit: PublishedText }
export interface MaterialProfile { placement: 'piece' | 'layer'; waterTypes: string[]; waterEffect: Knowledge<'inert' | 'tannins' | 'softening' | 'hardening' | 'conditional'>; preparation: PublishedText; defaultThicknessMm?: number; unit: PublishedText }
export interface SceneAsset { front: string; top: string; widthMm: number; depthMm: number; heightMm: number; anchor: 'center' | 'bottom'; style: 'diagram'; color: string }
export interface DiscoveryTraits { waterTypes: WaterType[]; filterLabels?: Record<Locale,Record<string,string|string[]>>; reproductionMode?: string; sizeClass?: string; socialPatterns?: string[]; bodyShapes?: string[]; distribution?: string; taxonomy?: Record<string,string>; husbandry?: Record<string,unknown> }
export interface CatalogEntity { parameterValues?:ParameterValue[]; publishedLocales?: Locale[]; id: string; revision: string; kind: EntityKind; active: boolean; scientificName: string; aliases: string[]; tags: string[]; colors: string[]; discovery?: DiscoveryTraits; locales: PublishedLocales<EntityLocale>; care?: CareProfile; plant?: PlantProfile; material?: MaterialProfile; media: { photo: string | null; photoStatus: 'verified' | 'missing'; attribution?: string }; scene: SceneAsset }
export interface CatalogCategory { id: string; names: Bilingual; kinds: EntityKind[]; order: number; active: boolean }
export interface PairRule { publishedLocales?: Locale[]; id: string; revision: string; a: string; b: string; enabled: boolean; visibility: 'recommended' | 'caution' | 'hidden'; decision: 'conditional' | 'caution' | 'conflict'; condition: PublishedText; reason: PublishedText; action: PublishedText; conditionKeys: ('cover' | 'separateFeeding' | 'shrimpletSurvival' | 'territory' | 'sexKnown')[] }
export interface ParameterDefinition { id:string; names:Bilingual; type:'number'|'text'|'boolean'|'enum'|'range'|'set'; unit?:string; kinds:EntityKind[]; options?:{id:string;names:Bilingual}[]; active:boolean; order:number }
export interface ParameterValue { definitionId:string; value:Knowledge<number|string|boolean|Range|string[]> }
export interface CatalogRelease { parameterDefinitions?:ParameterDefinition[]; publishedLocales?: Locale[]; id: string; schemaVersion: 1; createdAt: string; catalog: CatalogEntity[]; templates: never[]; pairRules: PairRule[]; categories: CatalogCategory[]; rulesetVersion: string; engineVersion: string; assetManifestVersion: string; capabilities: { designer: false }; localeRevisions: Record<Locale, string> }
export type ContentRelease = CatalogRelease;
