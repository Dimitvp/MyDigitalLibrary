// Mirrors the Application-layer DTOs (see src/MyDigitalLibrary.Application).
// Enums serialize as strings (JsonStringEnumConverter, Program.cs).

export type BookFormat = 'Physical' | 'Ebook' | 'Audiobook';
export type CoverType = 'Unknown' | 'Hardcover' | 'Paperback';
export type OwnershipStatus = 'Owned' | 'Borrowed' | 'LentOut' | 'Sold' | 'GivenAway';
export type AcquisitionMethod = 'Bought' | 'Gift' | 'Borrowed' | 'Inherited' | 'Downloaded';
export type ReadingStatus = 'Reading' | 'Finished' | 'Abandoned' | 'OnHold';

export interface Money {
  amount: number;
  currencyCode: string;
}

export interface Acquisition {
  acquiredOn: string; // yyyy-MM-dd
  method: AcquisitionMethod;
  price: Money | null;
  source: string | null;
}

export interface PhysicalLocation {
  room: string | null;
  shelf: string | null;
  box: string | null;
}

export interface LibraryItem {
  id: string;
  userId: string;
  editionId: string;
  format: BookFormat;
  status: OwnershipStatus;
  acquisition: Acquisition;
  location: PhysicalLocation | null;
  personalNote: string | null;
  workTitle: string;
  authorNames: readonly string[];
  coverImageUrl: string | null;
  readingStatus: ReadingStatus | null;
  readingStartedOn: string | null; // yyyy-MM-dd
  readingEndedOn: string | null; // yyyy-MM-dd
  language: string | null;
  genreNames: readonly string[];
  workId: string;
}

export interface Genre {
  id: string;
  name: string;
  nameEn: string | null;
}

export interface AuthorSummary {
  id: string;
  fullName: string;
}

export interface WorkDetail {
  id: string;
  title: string;
  originalTitle: string | null;
  description: string | null;
  firstPublicationYear: number | null;
  seriesId: string | null;
  seriesPosition: number | null;
  authors: readonly AuthorSummary[];
  myRating: number | null;
  myReview: string | null;
  genreNames: readonly string[];
}

export interface ProgressEntry {
  id: string;
  recordedAt: string;
  kind: 'page' | 'percent' | 'timestamp';
  page: number | null;
  percent: number | null;
  positionMinutes: number | null;
}

export interface ReadingSessionInfo {
  id: string;
  userId: string;
  libraryItemId: string;
  format: BookFormat;
  startedOn: string; // yyyy-MM-dd
  status: ReadingStatus;
  endedOn: string | null;
  abandonReason: string | null;
  progress: readonly ProgressEntry[];
}

export interface CreateWorkInput {
  title: string;
  originalTitle: string | null;
  description: string | null;
  firstPublicationYear: number | null;
  authorNames: readonly string[] | null;
  seriesName: string | null;
  seriesPosition: number | null;
  genreNames: readonly string[] | null;
}

export interface UpdateWorkRequest {
  title: string;
  originalTitle: string | null;
  description: string | null;
  firstPublicationYear: number | null;
  genreNames: readonly string[] | null;
  authorNames: readonly string[] | null;
}

export interface NestedEditionInput {
  isbn13: string | null;
  publisher: string | null;
  language: string | null;
  translator: string | null;
  publicationYear: number | null;
  pageCount: number | null;
  coverType: CoverType | null;
  narrator: string | null;
  durationMinutes: number | null;
  coverUrl: string | null;
}

export interface UpdateEditionRequest {
  isbn13: string | null;
  publisher: string | null;
  language: string | null;
  translator: string | null;
  publicationYear: number | null;
  pageCount: number | null;
  coverType: CoverType;
  narrator: string | null;
  durationMinutes: number | null;
}

export interface CreateLibraryItemRequest {
  editionId: string | null;
  work: CreateWorkInput | null;
  edition: NestedEditionInput | null;
  format: BookFormat;
  status: OwnershipStatus | null;
  acquisition: Acquisition;
  location: PhysicalLocation | null;
}

export interface Edition {
  id: string;
  workId: string;
  format: BookFormat;
  isbn13: string | null;
  publisher: string | null;
  language: string | null;
  translator: string | null;
  publicationYear: number | null;
  pageCount: number | null;
  coverType: CoverType;
  coverImageUrl: string | null;
  narrator: string | null;
  durationMinutes: number | null;
}

export interface WishlistEntry {
  id: string;
  userId: string;
  workId: string;
  preferredEditionId: string | null;
  desiredFormat: BookFormat;
  priority: number;
  maxPrice: Money | null;
  note: string | null;
  addedOn: string;
  isFulfilled: boolean;
  workTitle: string;
  authorNames: readonly string[];
}

export interface CreateWishlistEntryRequest {
  workId: string | null;
  work: CreateWorkInput | null;
  desiredFormat: BookFormat;
  priority: number;
  preferredEditionId: string | null;
  maxPrice: Money | null;
  note: string | null;
}

export interface BookMetadataCandidate {
  providerKey: string;
  title: string | null;
  originalTitle: string | null;
  authorNames: readonly string[];
  publisher: string | null;
  publicationYear: number | null;
  language: string | null;
  pageCount: number | null;
  description: string | null;
  coverUrl: string | null;
  seriesName: string | null;
  seriesPosition: number | null;
  genres: readonly string[];
}

export interface ImportLookupRequest {
  url: string | null;
  isbn: string | null;
}

export interface ImportLookupResult {
  isbn13: string;
  candidate: BookMetadataCandidate;
  alternates: readonly BookMetadataCandidate[];
}

export interface PagedResult<T> {
  items: readonly T[];
  page: number;
  pageSize: number;
  totalCount: number;
}

export interface AuthorBookCount {
  authorName: string;
  count: number;
}

export interface StatisticsDto {
  year: number;
  totalLibraryItems: number;
  byFormat: Record<string, number>;
  byStatus: Record<string, number>;
  booksFinishedThisYear: number;
  pagesReadThisYear: number;
  currentlyReadingCount: number;
  averageRating: number | null;
  topAuthors: readonly AuthorBookCount[];
}

export interface ProblemDetails {
  type?: string;
  title?: string;
  status?: number;
  detail?: string;
  errorCode?: string;
  [key: string]: unknown;
}
