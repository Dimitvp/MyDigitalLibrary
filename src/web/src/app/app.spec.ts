import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { provideTransloco } from '@jsverse/transloco';
import { App } from './app';
import { TranslocoHttpLoader } from './core/i18n/transloco-http-loader';

describe('App', () => {
  let httpMock: HttpTestingController;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [App],
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideRouter([]),
        provideTransloco({
          config: { availableLangs: ['bg', 'en'], defaultLang: 'bg', prodMode: true },
          loader: TranslocoHttpLoader,
        }),
      ],
    }).compileComponents();

    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => httpMock.verify());

  it('should create the app', () => {
    const fixture = TestBed.createComponent(App);
    fixture.detectChanges();
    httpMock.expectOne('/assets/i18n/bg.json').flush({ app: { title: 'My Digital Library' }, nav: { library: 'Library' } });

    expect(fixture.componentInstance).toBeTruthy();
  });

  it('renders the brand link and library nav item', () => {
    const fixture = TestBed.createComponent(App);
    fixture.detectChanges();
    httpMock.expectOne('/assets/i18n/bg.json').flush({ app: { title: 'My Digital Library' }, nav: { library: 'Library' } });
    fixture.detectChanges();

    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.querySelector('.brand')?.textContent).toContain('My Digital Library');
    expect(compiled.querySelector('nav a')?.textContent).toContain('Library');
  });
});
