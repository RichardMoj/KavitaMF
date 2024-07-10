import { HttpClient } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { environment } from 'src/environments/environment';
import { BookChapterItem } from './_models/book-chapter-item';
import { BookInfo } from './_models/book-info';

export interface BookPage {
  bookTitle: string;
  styles: string;
  html: string;
}

export interface FontFamily {
  /**
   * What the user should see
   */
  title: string;
  /**
   * The actual font face
   */
  family: string;
}

@Injectable({
  providedIn: 'root'
})
export class BookService {

  baseUrl = environment.apiUrl;

  constructor(private http: HttpClient) { }

  getFontFamilies(): Array<FontFamily> {
    return [{ title: 'default', family: 'default' }, { title: 'EBGaramond', family: 'EBGaramond' }, { title: 'Fira Sans', family: 'Fira_Sans' },
    { title: 'Lato', family: 'Lato' }, { title: 'Libre Baskerville', family: 'Libre_Baskerville' }, { title: 'Merriweather', family: 'Merriweather' },
    { title: 'Nanum Gothic', family: 'Nanum_Gothic' }, { title: 'RocknRoll One', family: 'RocknRoll_One' }, { title: 'Open Dyslexic', family: 'OpenDyslexic2' },
    { title: 'Comic Neue', family: 'Comic_Neue' }, { title: 'Atkison Hyperlegible', family: 'Atkison_Hyperlegible' }, { title: 'Lexend', family: 'Lexend' }, { title: 'Open Sans', family: 'Open_Sans' }, { title: 'Source Sans 3', family: 'Source_Sans_3' }, { title: 'Frank Ruhl Libre', family: 'Frank_Ruhl_Libre' }, { title: 'Comfortaa', family: 'Comfortaa' }];;
  }

  getBookChapters(chapterId: number) {
    return this.http.get<Array<BookChapterItem>>(this.baseUrl + 'book/' + chapterId + '/chapters');
  }

  getBookPage(chapterId: number, page: number) {
    return this.http.get<string>(this.baseUrl + 'book/' + chapterId + '/book-page?page=' + page, { responseType: 'text' as 'json' });
  }

  getBookInfo(chapterId: number) {
    return this.http.get<BookInfo>(this.baseUrl + 'book/' + chapterId + '/book-info');
  }

  getBookPageUrl(chapterId: number, page: number) {
    return this.baseUrl + 'book/' + chapterId + '/book-page?page=' + page;
  }
}
